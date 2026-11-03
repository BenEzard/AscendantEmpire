using AscendantEmpire.Application.Common;
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
/// <b>Only a camp delivers, and never into another camp</b> - checked here against <c>Settlement.Tier</c> rather than taken on trust from
/// <c>ICampParentResolver</c>, which is the only thing that writes a designation today. So there is no chain to unwind and no order in which the deliveries have to
/// run. A chain would do worse than strand stock halfway along itself for a Turn: empty the middle camp first and the lower one adds to a balance row already marked
/// for deletion, destroying that stock rather than moving it.
/// </para>
/// <para>
/// Both halves of the eligibility gate are read from the same projection, so "designated" and "connected" are decided against one snapshot of the empire rather than
/// against two reads that could disagree.
/// </para>
/// </remarks>
internal sealed class AssetBankDeliveryService(ILogger<AssetBankDeliveryService> logger) : IAssetBankDeliveryService
{
    public async Task<ServiceResult<AssetDeliveryOutcome>> DeliverCampProductionAsync(
        AscendantEmpireContext context,
        int gameId,
        int playerId,
        CancellationToken cancelToken = default)
    {
        ArgumentNullException.ThrowIfNull(context);

        // Ordered by Settlement id so the log and the rows written are the same for the same empire, run after run.
        List<DeliveryBankRow> bankRows = await context.AssetBanks
            .AsNoTracking()
            .Where(bank => bank.PlayerId == playerId && bank.Settlement.GameId == gameId)
            .OrderBy(bank => bank.SettlementId)
            .Select(bank => new DeliveryBankRow(bank.Id, bank.SettlementId, bank.Settlement.Tier, bank.NetworkKey, bank.Settlement.ParentSettlementId))
            .ToListAsync(cancelToken);

        List<CampDelivery> deliveries = FindEligibleDeliveries(bankRows);

        if (deliveries.Count == 0)
        {
            return ServiceResult<AssetDeliveryOutcome>.Succeed(new AssetDeliveryOutcome(0, 0));
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

        var outcome = new AssetDeliveryOutcome(0, 0);

        foreach (CampDelivery delivery in deliveries)
        {
            outcome = DeliverOneCamp(context, delivery, campBalancesByBankId, balanceByKey, outcome);
        }

        if (outcome.CampsEmptied > 0)
        {
            logger.LogInformation(
                "Player Id {PlayerId} in Game Id {GameId} delivered {BalancesMoved} balance row(s) from {CampsEmptied} of {EligibleCampCount} eligible camp(s) "
                + "into their parent settlements.",
                playerId, gameId, outcome.BalancesMoved, outcome.CampsEmptied, deliveries.Count);
        }

        return ServiceResult<AssetDeliveryOutcome>.Succeed(outcome);
    }

    /// <summary>
    /// The camps that both carry a designation and share their parent's network, which are the only ones that deliver.
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
            // A Settlement that is no longer a camp keeps its designation, and sweeping a village's whole store out of it would be far worse than the stale column.
            if (row.Tier != SettlementTier.Camp)
            {
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
                continue;
            }

            if (row.NetworkKey == Guid.Empty || row.NetworkKey != parent.NetworkKey)
            {
                continue;
            }

            deliveries.Add(new CampDelivery(row.AssetBankId, parent.AssetBankId));
        }

        return deliveries;
    }

    /// <summary>
    /// Moves one camp's whole store into its parent's bank, leaving the camp with no balance row at all.
    /// </summary>
    /// <remarks>
    /// The parent's row is added to where it already holds that Asset and created where it does not, and the newly created row goes back into
    /// <paramref name="balanceByKey"/> so a second camp feeding the same parent adds to it rather than inserting a duplicate of a composite key.
    /// </remarks>
    private static AssetDeliveryOutcome DeliverOneCamp(
        AscendantEmpireContext context,
        CampDelivery delivery,
        IReadOnlyDictionary<int, List<AssetBankBalance>> campBalancesByBankId,
        Dictionary<BalanceKey, AssetBankBalance> balanceByKey,
        AssetDeliveryOutcome outcome)
    {
        if (campBalancesByBankId.TryGetValue(delivery.CampAssetBankId, out List<AssetBankBalance>? campBalances) == false)
        {
            return outcome;
        }

        int balancesMoved = 0;

        foreach (AssetBankBalance campBalance in campBalances)
        {
            if (campBalance.Quantity > 0)
            {
                var parentKey = new BalanceKey(delivery.ParentAssetBankId, campBalance.AssetId);

                if (balanceByKey.TryGetValue(parentKey, out AssetBankBalance? parentBalance))
                {
                    parentBalance.Quantity += campBalance.Quantity;
                }
                else
                {
                    var created = new AssetBankBalance
                    {
                        AssetBankId = delivery.ParentAssetBankId,
                        AssetId = campBalance.AssetId,
                        Quantity = campBalance.Quantity
                    };

                    context.AssetBankBalances.Add(created);
                    balanceByKey[parentKey] = created;
                }

                balancesMoved++;
            }

            // Removed whether or not it held anything: a zero row and a missing row mean the same thing to every reader, so the camp is left with neither.
            context.AssetBankBalances.Remove(campBalance);
        }

        return balancesMoved == 0
            ? outcome
            : new AssetDeliveryOutcome(outcome.CampsEmptied + 1, outcome.BalancesMoved + balancesMoved);
    }

    /// <summary>One bank, with its Settlement's tier, what it is keyed to, and which Settlement that Settlement feeds.</summary>
    private sealed record DeliveryBankRow(int AssetBankId, int SettlementId, SettlementTier Tier, Guid NetworkKey, int? ParentSettlementId);

    /// <summary>One camp that will empty into one parent.</summary>
    private sealed record CampDelivery(int CampAssetBankId, int ParentAssetBankId);

    /// <summary>The composite key of a balance row, for looking one up without a second query.</summary>
    private readonly record struct BalanceKey(int AssetBankId, int AssetId);
}
