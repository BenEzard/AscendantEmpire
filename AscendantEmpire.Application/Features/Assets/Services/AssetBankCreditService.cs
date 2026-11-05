using AscendantEmpire.Application.Common;
using AscendantEmpire.Application.Data;
using AscendantEmpire.Application.Features.Assets.Classes;
using AscendantEmpire.Domain.Entities.Assets;
using AscendantEmpire.Domain.Enums.Assets;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

using static AscendantEmpire.Application.Common.ServiceResultBase;

namespace AscendantEmpire.Application.Features.Assets.Services;

/// <inheritdoc cref="IAssetBankCreditService" />
internal sealed class AssetBankCreditService(
    IAssetBankCapacityService capacityService,
    ILogger<AssetBankCreditService> logger) : IAssetBankCreditService
{
    public async Task<ServiceResult<IReadOnlyList<AssetCreditPlacement>>> CreditAsync(
        AscendantEmpireContext context,
        IReadOnlyList<AssetCredit> credits,
        CancellationToken cancelToken = default)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(credits);

        if (credits.Count == 0)
        {
            return ServiceResult<IReadOnlyList<AssetCreditPlacement>>.Succeed([]);
        }

        ServiceResult<Dictionary<int, CreditAsset>> assetsResult = await ReadAssetsAsync(context, credits, cancelToken);

        if (assetsResult.HasSucceeded == false)
        {
            return Propagate(assetsResult);
        }

        Dictionary<int, CreditAsset> assetsById = assetsResult.SuccessValue!;

        ServiceResult<List<CreditRoute>> routesResult = await ResolveRoutesAsync(context, credits, cancelToken);

        if (routesResult.HasSucceeded == false)
        {
            return Propagate(routesResult);
        }

        List<CreditRoute> routes = routesResult.SuccessValue!;

        int[] destinationSettlementIds = [.. routes.SelectMany(route => route.Banks).Select(bank => bank.SettlementId).Distinct()];

        ServiceResult<IReadOnlyDictionary<int, SettlementStorageCapacity>> capacities =
            await capacityService.GetSettlementCapacitiesAsync(context, destinationSettlementIds, cancelToken);

        if (capacities.HasSucceeded == false)
        {
            return Propagate(capacities);
        }

        Dictionary<BalanceKey, AssetBankBalance> balanceByKey = await ReadBalancesAsync(context, credits, routes, cancelToken);

        var placements = new List<AssetCreditPlacement>(credits.Count);

        for (int index = 0; index < credits.Count; index++)
        {
            placements.Add(PlaceOneCredit(
                context,
                credits[index],
                assetsById[credits[index].AssetId],
                routes[index],
                capacities.SuccessValue!,
                balanceByKey));
        }

        LogOutcome(credits, placements);

        return ServiceResult<IReadOnlyList<AssetCreditPlacement>>.Succeed(placements);
    }

    /// <summary>
    /// Reads the catalogue rows the credits name, and refuses the call where one cannot be credited at all.
    /// </summary>
    /// <remarks>
    /// Both refusals name every offender rather than the first, because a Turn's production is built in one batch: fixing them one call at a time would mean one
    /// failed Turn per bad Asset.
    /// </remarks>
    private static async Task<ServiceResult<Dictionary<int, CreditAsset>>> ReadAssetsAsync(
        AscendantEmpireContext context,
        IReadOnlyList<AssetCredit> credits,
        CancellationToken cancelToken)
    {
        int[] assetIds = [.. credits.Select(credit => credit.AssetId).Distinct()];

        Dictionary<int, CreditAsset> assetsById = await context.Assets
            .AsNoTracking()
            .Where(asset => assetIds.Contains(asset.Id))
            .Select(asset => new CreditAsset(asset.Id, asset.DisplayName, asset.StorageScope, asset.StorageClassId))
            .ToDictionaryAsync(asset => asset.AssetId, cancelToken);

        int[] missingAssetIds = [.. assetIds.Except(assetsById.Keys)];

        if (missingAssetIds.Length > 0)
        {
            return ServiceResult<Dictionary<int, CreditAsset>>.NotFound($"No Asset is in the catalogue for Id(s) {string.Join(", ", missingAssetIds)}, so nothing can be credited of them.");
        }

        CreditAsset[] unbankable = [.. assetsById.Values.Where(asset => asset.StorageScope != AssetStorageScope.SettlementBank)];

        if (unbankable.Length > 0)
        {
            // Not a shortfall of room. A Player or Derived Asset has no balance row anywhere, so there is nothing for a credit to add to and never will be.
            string offenders = string.Join(", ", unbankable.Select(asset => $"\"{asset.DisplayName}\" ({asset.StorageScope})"));

            return ServiceResult<Dictionary<int, CreditAsset>>.InternalServerError($"{offenders} is not kept in a settlement bank, so it cannot be credited to one.");
        }

        return ServiceResult<Dictionary<int, CreditAsset>>.Succeed(assetsById);
    }

    /// <summary>
    /// Works out which banks each credit may fill, in the order the tie-break walks them.
    /// </summary>
    /// <remarks>
    /// <para>
    /// One route per credit, index-aligned with them. Two credits arriving at the same Settlement get their own route rather than a shared one, which costs
    /// nothing and keeps the per-credit walk below free of lookups that could miss.
    /// </para>
    /// <para>
    /// <b>PlayerId is in the network predicate as well as the key</b>, because the index is <c>(PlayerId, NetworkKey)</c> and a key alone would let a conquest
    /// caught mid-recompute fill a rival's bank.
    /// </para>
    /// <para>
    /// An unkeyed bank reaches nothing but itself: <see cref="Guid.Empty"/> is the absence of a key rather than a network two banks share, so it is never grouped
    /// on.
    /// </para>
    /// <para>
    /// A credit whose whole network is excluded routes to no bank at all, which is a full network rather than a fault - the whole amount comes back unplaced.
    /// </para>
    /// </remarks>
    private static async Task<ServiceResult<List<CreditRoute>>> ResolveRoutesAsync(
        AscendantEmpireContext context,
        IReadOnlyList<AssetCredit> credits,
        CancellationToken cancelToken)
    {
        int[] playerIds = [.. credits.Select(credit => credit.PlayerId).Distinct()];

        // Every bank of every Player named, in one read: a Player holds settlements in one Game, and the set is bounded by how many they hold.
        List<CreditBankRow> bankRows = await context.AssetBanks
            .AsNoTracking()
            .Where(bank => playerIds.Contains(bank.PlayerId))
            .OrderBy(bank => bank.Id)
            .Select(bank => new CreditBankRow(bank.Id, bank.PlayerId, bank.SettlementId, bank.NetworkKey))
            .ToListAsync(cancelToken);

        Dictionary<SiteKey, CreditBankRow> siteByKey = bankRows.ToDictionary(row => new SiteKey(row.PlayerId, row.SettlementId));

        var missingSites = new List<string>();
        var routes = new List<CreditRoute>(credits.Count);

        foreach (AssetCredit credit in credits)
        {
            if (siteByKey.TryGetValue(new SiteKey(credit.PlayerId, credit.SettlementId), out CreditBankRow? site) == false)
            {
                // Still a not-found however much the rest of the Empire could hold: the credit arrives at this Settlement, and one that cannot bank cannot
                // receive.
                missingSites.Add($"Settlement {credit.SettlementId} has no asset bank belonging to Player {credit.PlayerId}, so nothing can be credited there.");
                routes.Add(new CreditRoute([]));

                continue;
            }

            IEnumerable<CreditBankRow> network = site.NetworkKey == Guid.Empty
                ? [site]
                : bankRows.Where(row => row.PlayerId == credit.PlayerId && row.NetworkKey == site.NetworkKey);

            routes.Add(new CreditRoute([.. network.Where(row => credit.ExcludedAssetBankIds.Contains(row.AssetBankId) == false)]));
        }

        if (missingSites.Count > 0)
        {
            return ServiceResult<List<CreditRoute>>.NotFound(missingSites.Distinct());
        }

        return ServiceResult<List<CreditRoute>>.Succeed(routes);
    }

    /// <summary>
    /// The balance rows the credits will write, keyed so one can be found without a second query.
    /// </summary>
    /// <remarks>
    /// Tracked, unlike every other read here: these are the rows being written, and the caller's save is what commits them. Rows already added to the context but
    /// not yet saved are merged in, because a query runs against the store and would not see them - and inserting a second row for a composite key the context is
    /// already holding would fail the save.
    /// </remarks>
    private static async Task<Dictionary<BalanceKey, AssetBankBalance>> ReadBalancesAsync(
        AscendantEmpireContext context,
        IReadOnlyList<AssetCredit> credits,
        IReadOnlyList<CreditRoute> routes,
        CancellationToken cancelToken)
    {
        int[] assetIds = [.. credits.Select(credit => credit.AssetId).Distinct()];
        int[] destinationBankIds = [.. routes.SelectMany(route => route.Banks).Select(bank => bank.AssetBankId).Distinct()];

        List<AssetBankBalance> balances = await context.AssetBankBalances
            .Where(balance => destinationBankIds.Contains(balance.AssetBankId) && assetIds.Contains(balance.AssetId))
            .ToListAsync(cancelToken);

        Dictionary<BalanceKey, AssetBankBalance> balanceByKey = balances.ToDictionary(balance => new BalanceKey(balance.AssetBankId, balance.AssetId));

        foreach (AssetBankBalance added in context.ChangeTracker
            .Entries<AssetBankBalance>()
            .Where(entry => entry.State == EntityState.Added)
            .Select(entry => entry.Entity))
        {
            if (destinationBankIds.Contains(added.AssetBankId) && assetIds.Contains(added.AssetId))
            {
                balanceByKey[new BalanceKey(added.AssetBankId, added.AssetId)] = added;
            }
        }

        return balanceByKey;
    }

    /// <summary>
    /// Places one credit across its route, and answers what landed and what did not.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The capacity is taken from as each bank is filled, so a later credit of the same class sees what this one placed. <see cref="SettlementStorageCapacity.Store"/>
    /// clamps to the same room the spread was planned against, and its answer is what is written rather than the plan, so the two cannot drift apart.
    /// </para>
    /// <para>A bank with no row for the Asset is given one, and only ever with a positive quantity - a bank that took nothing gets no row at all.</para>
    /// </remarks>
    private static AssetCreditPlacement PlaceOneCredit(
        AscendantEmpireContext context,
        AssetCredit credit,
        CreditAsset asset,
        CreditRoute route,
        IReadOnlyDictionary<int, SettlementStorageCapacity> capacityBySettlementId,
        Dictionary<BalanceKey, AssetBankBalance> balanceByKey)
    {
        Destination[] destinations =
        [
            .. route.Banks.Select(bank => new Destination(
                bank.AssetBankId,
                bank.SettlementId,
                RawHeld: balanceByKey.TryGetValue(new BalanceKey(bank.AssetBankId, credit.AssetId), out AssetBankBalance? held) ? held.Quantity : 0,
                RawRoom: capacityBySettlementId[bank.SettlementId].SpaceAvailable(asset.StorageClassId)))
        ];

        long[] fills = LevelUp(destinations, credit.Amount.RawValue);

        long rawPlaced = 0;

        for (int index = 0; index < destinations.Length; index++)
        {
            if (fills[index] <= 0)
            {
                continue;
            }

            Destination destination = destinations[index];
            long stored = capacityBySettlementId[destination.SettlementId].Store(asset.StorageClassId, fills[index]);

            if (stored <= 0)
            {
                continue;
            }

            var key = new BalanceKey(destination.AssetBankId, credit.AssetId);

            if (balanceByKey.TryGetValue(key, out AssetBankBalance? balance))
            {
                balance.Quantity += stored;
            }
            else
            {
                var created = new AssetBankBalance
                {
                    AssetBankId = destination.AssetBankId,
                    AssetId = credit.AssetId,
                    Quantity = stored
                };

                context.AssetBankBalances.Add(created);
                balanceByKey[key] = created;
            }

            rawPlaced += stored;
        }

        // Taken off the amount rather than off the plan, so the figure reported as lost is what the balances were actually short of receiving.
        return new AssetCreditPlacement(credit, asset.StorageClassId, rawPlaced, credit.Amount.RawValue - rawPlaced);
    }

    /// <summary>
    /// Spreads a credit across the banks by levelling: the emptiest are filled up to a common water-line and the fullest are left alone.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The mirror of the drawdown's <c>LevelDown</c>, and chosen for the same reason inverted. A proportional spread would give every bank a share of every
    /// credit, which would leave the Settlement that just emptied itself on a build order no better off than the one that spent nothing. Levelling hands the
    /// largest share to the bank holding least, so a network re-levels over the Turns that follow an expense.
    /// </para>
    /// <para>
    /// With banks holding 4, 30 and 50 and a credit of 40, the water-line falls at 37: the first two are filled to 37 each and the third is untouched.
    /// </para>
    /// <para>
    /// <b>Each bank is capped at its own room</b>, so the line is not a level every bank reaches. A bank that fills before the line stops there and the rest of
    /// the credit goes to the others, which is why the search below is over a capped fill rather than over a plain difference.
    /// </para>
    /// <para>
    /// <b>The leftover units go emptiest-first, then to the lower bank id.</b> A quantity is a scaled integer, so a water-line that divides exactly is the
    /// exception and a few raw units are normally left over. The order is the drawdown's tie-break inverted and fixed rather than incidental, so that the same
    /// credit against the same banks always writes the same rows.
    /// </para>
    /// </remarks>
    /// <returns>What to add to each bank, index-aligned with <paramref name="destinations"/>. It falls short of the amount only where the banks are full.</returns>
    private static long[] LevelUp(IReadOnlyList<Destination> destinations, long rawAmount)
    {
        long[] fills = new long[destinations.Count];
        long totalRoom = destinations.Sum(destination => destination.RawRoom);

        if (rawAmount >= totalRoom)
        {
            // Every bank fills, and whatever is left over is placed nowhere. Covers the full network, where the room is zero and so is every fill.
            for (int index = 0; index < destinations.Count; index++)
            {
                fills[index] = destinations[index].RawRoom;
            }

            return fills;
        }

        long waterLine = FindWaterLine(destinations, rawAmount);
        long outstanding = rawAmount;

        // Everything strictly below the line is filled first. The line is the lowest one that holds the whole credit, so what is left outstanding after this
        // pass is fewer units than there are banks the line runs through - never more.
        for (int index = 0; index < destinations.Count; index++)
        {
            fills[index] = FillTo(destinations[index], waterLine - 1);
            outstanding -= fills[index];
        }

        // The remainder is one raw unit each into the banks the line runs through, emptiest first.
        foreach (int index in InFillOrder(destinations))
        {
            if (outstanding <= 0)
            {
                break;
            }

            Destination destination = destinations[index];

            if (fills[index] >= destination.RawRoom || destination.RawHeld + fills[index] >= waterLine)
            {
                continue;
            }

            fills[index]++;
            outstanding--;
        }

        return fills;
    }

    /// <summary>
    /// The lowest level at which filling every bank up to it, each capped at its own room, would hold the whole credit.
    /// </summary>
    /// <remarks>
    /// Binary searched rather than walked, because a quantity is a scaled integer and the line can sit anywhere in a range of billions. What a level absorbs only
    /// rises as the level rises, which is what makes the search sound: at zero it is nothing, and at the fullest bank's own ceiling it is the whole of the room -
    /// which the caller has already established is more than the credit - so the line lies between the two and the search cannot run off either end.
    /// </remarks>
    private static long FindWaterLine(IReadOnlyList<Destination> destinations, long rawAmount)
    {
        long low = 0;
        long high = destinations.Max(destination => destination.RawHeld + destination.RawRoom);

        while (low < high)
        {
            // Rounded down, so a two-wide range tries the lower candidate and the loop cannot sit still on the upper one.
            long candidate = low + ((high - low) / 2);

            if (SumFillTo(destinations, candidate) >= rawAmount)
            {
                high = candidate;
            }
            else
            {
                low = candidate + 1;
            }
        }

        return low;
    }

    /// <summary>
    /// How much the banks would absorb in being filled up to the given level, which is what the level costs the credit.
    /// </summary>
    private static long SumFillTo(IReadOnlyList<Destination> destinations, long level)
    {
        return destinations.Sum(destination => FillTo(destination, level));
    }

    /// <summary>
    /// How much one bank would absorb in being filled up to the given level, bounded by the room it has.
    /// </summary>
    private static long FillTo(Destination destination, long level)
    {
        return Math.Min(destination.RawRoom, Math.Max(0, level - destination.RawHeld));
    }

    /// <summary>
    /// The banks in the order the leftover units are handed out: emptiest first, then the lower bank id.
    /// </summary>
    /// <remarks>
    /// Indices rather than the banks themselves, because the fills they belong to are index-aligned with the route and reordering would separate the two.
    /// </remarks>
    private static int[] InFillOrder(IReadOnlyList<Destination> destinations)
    {
        return
        [
            .. Enumerable.Range(0, destinations.Count)
                .OrderBy(index => destinations[index].RawHeld)
                .ThenBy(index => destinations[index].AssetBankId)
        ];
    }

    /// <summary>
    /// Records what the batch placed, and names each credit the network had no room for in full.
    /// </summary>
    /// <remarks>
    /// Named one credit at a time rather than summarised, because which Asset was lost is the whole content of it. A caller that knows more than this service
    /// does - which camp the stock came out of, say - logs its own line as well; this one is what a bare producer leaves behind.
    /// </remarks>
    private void LogOutcome(IReadOnlyList<AssetCredit> credits, IReadOnlyList<AssetCreditPlacement> placements)
    {
        long rawPlaced = placements.Sum(placement => placement.RawPlaced);
        long rawUnplaced = placements.Sum(placement => placement.RawUnplaced);

        foreach (AssetCreditPlacement placement in placements.Where(placement => placement.RawUnplaced > 0))
        {
            logger.LogInformation(
                "Settlement Id {SettlementId} and its network had no room for {RawUnplaced} (scaled) of Asset Id {AssetId} for Player Id {PlayerId}, "
                + "because storage class Id {StorageClassId} was full.",
                placement.Credit.SettlementId, placement.RawUnplaced, placement.Credit.AssetId, placement.Credit.PlayerId, placement.StorageClassId);
        }

        logger.LogInformation(
            "Credited {RawPlaced} (scaled) from {CreditCount} credit(s), with {RawUnplaced} (scaled) placed nowhere for want of storage.",
            rawPlaced,
            credits.Count,
            rawUnplaced);
    }

    /// <summary>
    /// Carries one of the helpers' failures out as the call's own, with its status intact.
    /// </summary>
    /// <remarks>
    /// The statuses are the point: a caller has to tell an Asset that is not in the catalogue from one that can never be banked, and collapsing both into a
    /// generic failure would hide which.
    /// </remarks>
    private static ServiceResult<IReadOnlyList<AssetCreditPlacement>> Propagate(ServiceResultBase failure)
    {
        return new ServiceResult<IReadOnlyList<AssetCreditPlacement>>
        {
            StatusCode = failure.StatusCode,
            Errors = failure.Errors,
            ErrorDetail = failure.ErrorDetail
        };
    }

    /// <summary>One catalogue Asset a credit names: where its figure is kept, and the class it is kept under.</summary>
    private sealed record CreditAsset(int AssetId, string DisplayName, AssetStorageScope StorageScope, int StorageClassId);

    /// <summary>One bank, with the Settlement whose capacity bounds it and the network it sits in.</summary>
    private sealed record CreditBankRow(int AssetBankId, int PlayerId, int SettlementId, Guid NetworkKey);

    /// <summary>The banks one credit may fill, in ascending bank id.</summary>
    private sealed record CreditRoute(IReadOnlyList<CreditBankRow> Banks);

    /// <summary>One bank a credit is being spread over, with what it holds of the Asset and how much more it can take.</summary>
    private sealed record Destination(int AssetBankId, int SettlementId, long RawHeld, long RawRoom);

    /// <summary>A Player's Settlement, which is what locates the bank a credit arrives at.</summary>
    private readonly record struct SiteKey(int PlayerId, int SettlementId);

    /// <summary>The composite key of a balance row, for looking one up without a second query.</summary>
    private readonly record struct BalanceKey(int AssetBankId, int AssetId);
}
