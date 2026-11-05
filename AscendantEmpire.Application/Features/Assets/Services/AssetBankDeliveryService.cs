using AscendantEmpire.Application.Data;
using AscendantEmpire.Application.Features.Assets.Classes;
using AscendantEmpire.Application.Helpers.ScaledNumerics;
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
/// <para>
/// <b>Where the stock lands is <see cref="IAssetBankCreditService"/>'s business, not this service's.</b> One credit is raised per camp balance row, against the
/// parent Settlement, and the spread is what decides which bank in the parent's network actually takes it.
/// </para>
/// </remarks>
internal sealed class AssetBankDeliveryService(
    IAssetBankCreditService creditService,
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

        // Tracked: these are the rows being emptied, and the caller's save is what commits them.
        List<AssetBankBalance> campBalances = await context.AssetBankBalances
            .Where(balance => campBankIds.Contains(balance.AssetBankId))
            .ToListAsync(cancelToken);

        // Every camp bank the Player holds, not just the delivering ones, because that is the set the spread must not fill. A delivery exists to get stock out of
        // a small store and into a large one, so putting it into another camp's store would gain nothing - and the source's own bank is excluded by being one.
        int[] everyCampBankId = [.. bankRows.Where(row => row.Tier == SettlementTier.Camp).Select(row => row.AssetBankId)];

        var credits = new List<AssetCredit>();
        var sources = new List<CampSource>();

        BuildCredits(context, deliveries, campBalances, everyCampBankId, playerId, credits, sources);

        if (credits.Count == 0)
        {
            return ServiceResult<AssetDeliveryOutcome>.Succeed(new AssetDeliveryOutcome(0, 0, 0));
        }

        ServiceResult<IReadOnlyList<AssetCreditPlacement>> credited = await creditService.CreditAsync(context, credits, cancelToken);

        if (credited.HasSucceeded == false)
        {
            return ServiceResult<AssetDeliveryOutcome>.Fail(credited.Errors);
        }

        AssetDeliveryOutcome outcome = ApplyPlacements(context, deliveries, sources, credited.SuccessValue!);

        if (outcome.CampsEmptied > 0 || outcome.BalancesHeldBack > 0)
        {
            logger.LogInformation(
                "Player Id {PlayerId} in Game Id {GameId} delivered {BalancesMoved} balance row(s) from {CampsEmptied} of {EligibleCampCount} eligible camp(s) "
                + "into the networks their parent settlements sit in, with {BalancesHeldBack} row(s) held back for want of storage.",
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

            deliveries.Add(new CampDelivery(row.AssetBankId, row.SettlementId, parent.SettlementId));
        }

        return deliveries;
    }

    /// <summary>
    /// Raises one credit per camp balance row, and remembers which row each credit came out of.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Ascending Asset id within each camp</b>, because the credits are placed in the order they are given and the parent's store runs out partway down the
    /// list. That order decides what survives a full parent, and it is arbitrary: a hunting camp delivering into a full village delivers its bones and its hides
    /// and loses its meat, because that is the order the catalogue declares them in. Reporting the loss is RS-51, and the ordering is deliberately left as it is
    /// until then.
    /// </para>
    /// <para>
    /// A row standing at nothing is removed rather than credited: a zero row and a missing row mean the same thing to every reader, and a credit cannot be built
    /// for a non-positive amount in any case.
    /// </para>
    /// </remarks>
    private static void BuildCredits(
        AscendantEmpireContext context,
        IReadOnlyList<CampDelivery> deliveries,
        IReadOnlyCollection<AssetBankBalance> campBalances,
        IReadOnlyCollection<int> everyCampBankId,
        int playerId,
        List<AssetCredit> credits,
        List<CampSource> sources)
    {
        Dictionary<int, List<AssetBankBalance>> campBalancesByBankId = campBalances
            .GroupBy(balance => balance.AssetBankId)
            .ToDictionary(group => group.Key, group => group.OrderBy(balance => balance.AssetId).ToList());

        foreach (CampDelivery delivery in deliveries)
        {
            if (campBalancesByBankId.TryGetValue(delivery.CampAssetBankId, out List<AssetBankBalance>? balances) == false)
            {
                continue;
            }

            foreach (AssetBankBalance balance in balances)
            {
                if (balance.Quantity <= 0)
                {
                    context.AssetBankBalances.Remove(balance);

                    continue;
                }

                credits.Add(AssetCredit.AtSettlement(
                    playerId,
                    delivery.ParentSettlementId,
                    balance.AssetId,
                    ScaledAmount.FromRaw(balance.Quantity),
                    everyCampBankId));

                sources.Add(new CampSource(delivery, balance));
            }
        }
    }

    /// <summary>
    /// Takes out of each camp row what the network actually received, and counts what it refused.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>The clamp is why a camp's row is not always removed.</b> A row the network took in full goes, because a zero row and a missing row mean the same thing to
    /// every reader; a row it could only take part of stays, holding a real quantity for the camp to offer again next Turn. Destroying that remainder is the one
    /// thing a delivery must not do.
    /// </para>
    /// <para>
    /// Each refused row is logged on its own, because which Asset was refused is the whole content of it: the aggregate cannot say whether a hunting Camp lost its
    /// bones or its meat. The camp it came out of is logged here rather than by the credit service, which has no way to know it.
    /// </para>
    /// </remarks>
    private AssetDeliveryOutcome ApplyPlacements(
        AscendantEmpireContext context,
        IReadOnlyList<CampDelivery> deliveries,
        IReadOnlyList<CampSource> sources,
        IReadOnlyList<AssetCreditPlacement> placements)
    {
        var movedByCampBankId = new Dictionary<int, int>();
        var heldBackByCampBankId = new Dictionary<int, int>();

        for (int index = 0; index < placements.Count; index++)
        {
            AssetCreditPlacement placement = placements[index];
            CampSource source = sources[index];
            int campAssetBankId = source.Delivery.CampAssetBankId;

            if (placement.RawPlaced > 0)
            {
                source.Balance.Quantity -= placement.RawPlaced;
                movedByCampBankId[campAssetBankId] = movedByCampBankId.GetValueOrDefault(campAssetBankId) + 1;
            }

            if (placement.RawUnplaced <= 0)
            {
                // Taken in full, so nothing is left to keep.
                context.AssetBankBalances.Remove(source.Balance);

                continue;
            }

            heldBackByCampBankId[campAssetBankId] = heldBackByCampBankId.GetValueOrDefault(campAssetBankId) + 1;

            logger.LogInformation(
                "Settlement Id {ParentSettlementId} and its network had no room for {RawHeldBack} (scaled) of Asset Id {AssetId} from the camp at Settlement Id "
                + "{CampSettlementId}, because storage class Id {StorageClassId} was full.",
                source.Delivery.ParentSettlementId,
                placement.RawUnplaced,
                source.Balance.AssetId,
                source.Delivery.CampSettlementId,
                placement.StorageClassId);
        }

        // A camp counts as emptied only where the network took everything, which is what makes the figure mean "this camp has nothing left" in the log.
        int campsEmptied = deliveries.Count(delivery =>
            movedByCampBankId.GetValueOrDefault(delivery.CampAssetBankId) > 0
            && heldBackByCampBankId.GetValueOrDefault(delivery.CampAssetBankId) == 0);

        return new AssetDeliveryOutcome(campsEmptied, movedByCampBankId.Values.Sum(), heldBackByCampBankId.Values.Sum());
    }

    /// <summary>One bank, with its Settlement's tier, what it is keyed to, and which Settlement that Settlement feeds.</summary>
    private sealed record DeliveryBankRow(int AssetBankId, int SettlementId, SettlementTier Tier, Guid NetworkKey, int? ParentSettlementId);

    /// <summary>One Camp that will deliver into one parent, named by the parent's Settlement because that is where the credit arrives.</summary>
    /// <remarks>The parent's bank is deliberately not here: which bank in its network takes the stock is the credit service's to decide, not this one's.</remarks>
    private sealed record CampDelivery(int CampAssetBankId, int CampSettlementId, int ParentSettlementId);

    /// <summary>The camp row one credit was raised out of, so what the network took can be taken off it again.</summary>
    private sealed record CampSource(CampDelivery Delivery, AssetBankBalance Balance);
}
