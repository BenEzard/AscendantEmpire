using AscendantEmpire.Application.Data;
using AscendantEmpire.Application.Features.Assets.Classes;
using AscendantEmpire.Domain.Entities.Assets;
using AscendantEmpire.Domain.Enums.Settlements;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

using static AscendantEmpire.Application.Common.ServiceResultBase;

namespace AscendantEmpire.Application.Features.Assets.Services;

/// <inheritdoc cref="IAssetBankDeliveryService" />
/// <remarks>
/// <para>
/// <b>Only a Camp delivers, and never into another Camp</b>.
/// </para>
/// <para>
/// Both halves of the eligibility gate are read from the same projection, so "designated" and "connected" are decided against one snapshot of the Empire rather than against two reads that could disagree.
/// </para>
/// </remarks>
internal sealed class AssetBankDeliveryService(
    IAssetBankCapacityService capacityService,
    ILogger<AssetBankDeliveryService> logger) : IAssetBankDeliveryService
{
    public async Task<ServiceResult<AssetDeliveryOutcome>> DeliverCampProductionAsync(
        AscendantEmpireContext context,
        int gameId,
        int playerId,
        CancellationToken cancelToken = default)
    {
        ArgumentNullException.ThrowIfNull(context);

        // Ordered by Settlement id so the log and the rows written are the same for the same Empire, run after run.
        List<DeliveryBankRow> bankRows = await context.AssetBanks
            .AsNoTracking()
            .Where(bank => bank.PlayerId == playerId && bank.Settlement.GameId == gameId)
            .OrderBy(bank => bank.SettlementId)
            .Select(bank => new DeliveryBankRow(bank.Id, bank.SettlementId, bank.Settlement.Tier, bank.NetworkKey, bank.Settlement.ParentSettlementId))
            .ToListAsync(cancelToken);

        List<CampDelivery> deliveries = FindEligibleDeliveries(bankRows);

        if (deliveries.Count == 0)
        {
            return ServiceResult<AssetDeliveryOutcome>.Succeed(new AssetDeliveryOutcome(0, 0, 0));
        }

        int[] campBankIds = [.. deliveries.Select(delivery => delivery.CampAssetBankId)];
        int[] involvedBankIds = [.. campBankIds.Concat(deliveries.Select(delivery => delivery.ParentAssetBankId)).Distinct()];

        // Tracked: these are the rows being moved, and the caller's save is what commits them.
        List<AssetBankBalance> balances = await context.AssetBankBalances
            .Where(balance => involvedBankIds.Contains(balance.AssetBankId))
            .ToListAsync(cancelToken);

        Dictionary<int, List<AssetBankBalance>> campBalancesByBankId = balances
            .Where(balance => campBankIds.Contains(balance.AssetBankId))
            .GroupBy(balance => balance.AssetBankId)
            .ToDictionary(group => group.Key, group => group.OrderBy(balance => balance.AssetId).ToList());

        Dictionary<BalanceKey, AssetBankBalance> balanceByKey = balances.ToDictionary(balance => new BalanceKey(balance.AssetBankId, balance.AssetId));

        // The balances above are tracked entities, so the storage class an Asset is kept under comes from a keyed projection of its own rather than off the same
        // row. Capacity binds against the class, so a clamp cannot be worked out without it.
        int[] involvedAssetIds = [.. balances.Select(balance => balance.AssetId).Distinct()];

        Dictionary<int, int> storageClassIdByAssetId = await context.Assets
            .AsNoTracking()
            .Where(asset => involvedAssetIds.Contains(asset.Id))
            .Select(asset => new { asset.Id, asset.StorageClassId })
            .ToDictionaryAsync(asset => asset.Id, asset => asset.StorageClassId, cancelToken);

        int[] parentSettlementIds = [.. deliveries.Select(delivery => delivery.ParentSettlementId).Distinct()];

        ServiceResult<IReadOnlyDictionary<int, SettlementStorageCapacity>> capacities =
            await capacityService.GetSettlementCapacitiesAsync(context, parentSettlementIds, cancelToken);

        if (capacities.HasSucceeded == false)
        {
            return ServiceResult<AssetDeliveryOutcome>.Fail(capacities.Errors);
        }

        var heldBack = new List<HeldBackRow>();
        var outcome = new AssetDeliveryOutcome(0, 0, 0);

        foreach (CampDelivery delivery in deliveries)
        {
            outcome = DeliverFromOneCamp(
                context,
                delivery,
                campBalancesByBankId,
                balanceByKey,
                storageClassIdByAssetId,
                capacities.SuccessValue![delivery.ParentSettlementId],
                heldBack,
                outcome);
        }

        foreach (HeldBackRow row in heldBack)
        {
            // Named one row at a time rather than summarised, because which Asset was refused is the whole content of it: the aggregate below cannot say
            // whether a hunting Camp lost its bones or its meat. This is what a player-facing report would be built from.
            logger.LogInformation(
                "Settlement Id {ParentSettlementId} had no room for {RawHeldBack} (scaled) of Asset Id {AssetId} from the camp at Settlement Id "
                + "{CampSettlementId}, because storage class Id {StorageClassId} was full.",
                row.ParentSettlementId, row.RawHeldBack, row.AssetId, row.CampSettlementId, row.StorageClassId);
        }

        if (outcome.CampsEmptied > 0 || outcome.BalancesHeldBack > 0)
        {
            logger.LogInformation(
                "Player Id {PlayerId} in Game Id {GameId} delivered {BalancesMoved} balance row(s) from {CampsEmptied} of {EligibleCampCount} eligible camp(s) "
                + "into their parent settlements, with {BalancesHeldBack} row(s) held back for want of storage.",
                playerId, gameId, outcome.BalancesMoved, outcome.CampsEmptied, deliveries.Count, outcome.BalancesHeldBack);
        }

        return ServiceResult<AssetDeliveryOutcome>.Succeed(outcome);
    }

    /// <summary>
    /// The Camps that both carry a designation and share their parent's network, which are the only ones that deliver.
    /// </summary>
    /// <remarks>
    /// A parent with no bank in this set belongs to somebody else now, so the camp is not eligible - and <c>Guid.Empty</c> is the absence of a key rather than a network,
    /// so two unkeyed banks are not connected however equal their columns look.
    ///
    /// Both tiers are checked, not just the designation, because the designation outlives whatever set it: nothing clears it when a Settlement changes tier.
    /// </remarks>
    private static List<CampDelivery> FindEligibleDeliveries(IReadOnlyCollection<DeliveryBankRow> bankRows)
    {
        Dictionary<int, DeliveryBankRow> bankBySettlementId = bankRows.ToDictionary(row => row.SettlementId);

        var deliveries = new List<CampDelivery>();

        foreach (DeliveryBankRow row in bankRows)
        {
            if (row.Tier != SettlementTier.Camp)
            {
                // A Settlement that is no longer a Camp keeps its designation, and sweeping a village's whole store out of it would be far worse than the stale column.
                continue;
            }

            if (row.ParentSettlementId is not int parentSettlementId)
            {
                continue;
            }

            if (bankBySettlementId.TryGetValue(parentSettlementId, out DeliveryBankRow? parent) == false)
            {
                continue;
            }

            if (parent.Tier == SettlementTier.Camp)
            {
                // Should not happen, but if it does, the parent is not a Settlement that can receive deliveries.
                continue;
            }

            if (row.NetworkKey == Guid.Empty || row.NetworkKey != parent.NetworkKey)
            {
                continue;
            }

            deliveries.Add(new CampDelivery(row.AssetBankId, row.SettlementId, parent.AssetBankId, parent.SettlementId));
        }

        return deliveries;
    }

    /// <summary>
    /// Moves as much of one camp's store into its parent's bank as the parent can hold, leaving the camp whatever would not fit.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The parent's row is added to where it already holds that Asset and created where it does not, and the newly created row goes back into
    /// <paramref name="balanceByKey"/> so a second camp feeding the same parent adds to it rather than inserting a duplicate of a composite key.
    /// </para>
    /// <para>
    /// <b>The clamp is why a camp's row is not always removed.</b> A row the parent took in full goes, because a zero row and a missing row mean the same thing to
    /// every reader; a row it could only take part of stays, holding a real quantity for the camp to offer again next Turn. Destroying that remainder is the one
    /// thing a delivery must not do.
    /// </para>
    /// <para>
    /// <paramref name="parentCapacity"/> is taken from as each row lands, so several Assets of one storage class cannot each be told the whole store is free - and
    /// a second camp feeding this parent sees what the first delivered.
    /// </para>
    /// </remarks>
    private static AssetDeliveryOutcome DeliverFromOneCamp(
        AscendantEmpireContext context,
        CampDelivery delivery,
        IReadOnlyDictionary<int, List<AssetBankBalance>> campBalancesByBankId,
        Dictionary<BalanceKey, AssetBankBalance> balanceByKey,
        IReadOnlyDictionary<int, int> storageClassIdByAssetId,
        SettlementStorageCapacity parentCapacity,
        List<HeldBackRow> heldBack,
        AssetDeliveryOutcome outcome)
    {
        if (campBalancesByBankId.TryGetValue(delivery.CampAssetBankId, out List<AssetBankBalance>? campBalances) == false)
        {
            return outcome;
        }

        int balancesMoved = 0;
        int balancesHeldBack = 0;

        foreach (AssetBankBalance campBalance in campBalances)
        {
            int storageClassId = storageClassIdByAssetId[campBalance.AssetId];

            long delivered = campBalance.Quantity > 0
                ? parentCapacity.Store(storageClassId, campBalance.Quantity)
                : 0;

            if (delivered > 0)
            {
                var parentKey = new BalanceKey(delivery.ParentAssetBankId, campBalance.AssetId);

                if (balanceByKey.TryGetValue(parentKey, out AssetBankBalance? parentBalance))
                {
                    parentBalance.Quantity += delivered;
                }
                else
                {
                    var created = new AssetBankBalance
                    {
                        AssetBankId = delivery.ParentAssetBankId,
                        AssetId = campBalance.AssetId,
                        Quantity = delivered
                    };

                    context.AssetBankBalances.Add(created);
                    balanceByKey[parentKey] = created;
                }

                balancesMoved++;
            }

            if (delivered < campBalance.Quantity)
            {
                heldBack.Add(new HeldBackRow(
                    delivery.CampSettlementId,
                    delivery.ParentSettlementId,
                    campBalance.AssetId,
                    storageClassId,
                    campBalance.Quantity - delivered));

                campBalance.Quantity -= delivered;
                balancesHeldBack++;

                continue;
            }

            // Taken in full, so nothing is left to keep. An empty row goes the same way: a zero row and a missing row mean the same thing to every reader.
            context.AssetBankBalances.Remove(campBalance);
        }

        // A camp counts as emptied only where the parent took everything, which is what makes the figure mean "this camp has nothing left" in the log.
        int campsEmptied = balancesMoved > 0 && balancesHeldBack == 0 ? 1 : 0;

        return new AssetDeliveryOutcome(
            outcome.CampsEmptied + campsEmptied,
            outcome.BalancesMoved + balancesMoved,
            outcome.BalancesHeldBack + balancesHeldBack);
    }

    /// <summary>One bank, with its Settlement's tier, what it is keyed to, and which Settlement that Settlement feeds.</summary>
    private sealed record DeliveryBankRow(int AssetBankId, int SettlementId, SettlementTier Tier, Guid NetworkKey, int? ParentSettlementId);

    /// <summary>One Camp that will deliver into one parent, with the parent's Settlement because that is what capacity binds against.</summary>
    private sealed record CampDelivery(int CampAssetBankId, int CampSettlementId, int ParentAssetBankId, int ParentSettlementId);

    /// <summary>One quantity a parent had no room for, and what bound.</summary>
    /// <remarks>
    /// Collected per row rather than counted, because the count alone cannot say which Asset was refused - and that is the one thing a player would need to be
    /// told. Logged today; the player-facing report is its own task.
    /// </remarks>
    private sealed record HeldBackRow(int CampSettlementId, int ParentSettlementId, int AssetId, int StorageClassId, long RawHeldBack);

    /// <summary>The composite key of a balance row, for looking one up without a second query.</summary>
    private readonly record struct BalanceKey(int AssetBankId, int AssetId);
}
