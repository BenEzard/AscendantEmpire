using AscendantEmpire.Application.Data;
using AscendantEmpire.Application.Features.Maps.Biomes.Classes;
using AscendantEmpire.Application.Features.Maps.CampaignMaps.Classes;
using AscendantEmpire.Application.Features.Maps.CampaignMaps.Services;
using AscendantEmpire.Application.Features.Maps.Depletion.Classes;
using AscendantEmpire.Application.Features.Maps.MapResources.Services;
using AscendantEmpire.Application.Helpers.ScaledNumerics;
using AscendantEmpire.BackendContracts.Biomes;
using AscendantEmpire.BackendContracts.CampaignMaps;
using AscendantEmpire.BackendContracts.CampaignMaps.GetPlayerCampaignMap;
using AscendantEmpire.Domain.Entities.Games;
using AscendantEmpire.Domain.Enums.Maps;

using Microsoft.EntityFrameworkCore;

namespace AscendantEmpire.Application.Features.Maps.Depletion.Services;

/// <inheritdoc cref="ITileFoodDisclosureReader" />
internal sealed class TileFoodDisclosureReader(
    ISustenanceAssetReader sustenanceAssetReader,
    ISupplySetMembershipReader supplySetMembershipReader) : ITileFoodDisclosureReader
{
    public async Task<TileFoodDisclosure> GetAsync(
        AscendantEmpireContext context,
        int gameId,
        int playerId,
        SightTerrain tile,
        PlayerSustenanceValues values,
        CampaignMapDepletion? depletion,
        TileRivers? rivers,
        CancellationToken cancelToken = default)
    {
        int? foodSupplySetId = await GetFoodSupplySetIdAsync(context, gameId, playerId, tile.CampaignMapId, cancelToken);

        if (foodSupplySetId is null)
        {
            return TileFoodDisclosure.None;
        }

        IReadOnlyDictionary<SustenanceResource, int> sustenanceAssetIds = await sustenanceAssetReader.GetAssetIdsAsync(context, cancelToken);

        IReadOnlyDictionary<int, IReadOnlyDictionary<int, long>> membershipsBySetId = await supplySetMembershipReader.GetAmountsPerUnitAsync(
            context,
            [foodSupplySetId.Value],
            cancelToken);

        IReadOnlyDictionary<int, long> amountPerUnitByAssetId = membershipsBySetId.GetValueOrDefault(foodSupplySetId.Value, new Dictionary<int, long>());

        IReadOnlyList<EstimatedPatch> patches = await ReadKnownPatchesAsync(context, gameId, playerId, tile.CampaignMapId, cancelToken);

        TileMapResourceStock[] stocks = [.. patches.Select(patch => new TileMapResourceStock(patch.InstanceId, patch.AssetId, patch.EstimatedAmount, patch.IsYieldEnabled))];

        IReadOnlyCollection<FoodSource> sources = TileFoodSources.GetAvailable(
            CampaignTileTerrain.IsWater(tile.BiomeIsWater, tile.GeographyIsWater),
            values,
            tile.BiomeType,
            depletion,
            sustenanceAssetIds,
            amountPerUnitByAssetId,
            stocks,
            rivers);

        Dictionary<int, string> patchNamesByInstanceId = patches
            .GroupBy(patch => patch.InstanceId)
            .ToDictionary(group => group.Key, group => group.First().DisplayName);

        // Summed raw and converted once, so the total is exactly the sum of its parts rather than the sum of four roundings of them.
        ScaledAmount total = sources.Aggregate(ScaledAmount.Zero, (sum, source) => sum + source.ContributionInNeedUnits);

        TileFoodContribution[] contributions =
        [
            .. sources.Select(source => new TileFoodContribution
            {
                Label = source.Key.SourceType == FoodSourceType.Tile
                    ? source.Key.Resource!.Value.ToString()
                    : patchNamesByInstanceId.GetValueOrDefault(source.Key.MapResourceInstanceId!.Value, string.Empty),
                IndividualsFed = source.ContributionInNeedUnits.ToDecimal(),
                IsEstimated = source.Key.SourceType == FoodSourceType.MapResource
            })
        ];

        return new TileFoodDisclosure(total.ToDecimal(), contributions.Any(contribution => contribution.IsEstimated), contributions);
    }

    /// <summary>
    /// The food <c>SupplySet</c> to price the tile against: the one the Forces standing on it eat from, or the Player's own commonest where none is.
    /// </summary>
    /// <remarks>
    /// The on-tile rule first, so the figure agrees with the support colour, which is forecast against the set the largest part of that tile's need belongs
    /// to. Falling back to the Player's Forces elsewhere keeps an empty tile priced by a real diet rather than by one ration of everything, which would read
    /// a berry patch as feeding half again as many as it does. Both come to the same set while only one Food set is seeded.
    /// </remarks>
    /// <returns><see langword="null"/> when the Player has nothing left that eats, so there is no diet to measure the tile's food in.</returns>
    private static async Task<int?> GetFoodSupplySetIdAsync(
        AscendantEmpireContext context,
        int gameId,
        int playerId,
        int tileId,
        CancellationToken cancelToken)
    {
        var rows = await context.UnitInstances
            .AsNoTracking()
            .Where(unit => unit.GameId == gameId)
            .Where(unit => unit.PlayerId == playerId)
            .Where(unit => unit.HealthyCount + unit.InjuredCount > 0)
            .Where(unit => unit.BattleMapTileId == null)
            .Where(unit => unit.UnitTemplate.Compiled!.FoodSupplySetId != null && unit.UnitTemplate.Compiled!.FoodQuantity > 0)
            .Select(unit => new
            {
                unit.CampaignMapTileId,
                SetId = unit.UnitTemplate.Compiled!.FoodSupplySetId!.Value,
                Individuals = unit.HealthyCount + unit.InjuredCount,
                Quantity = unit.UnitTemplate.Compiled!.FoodQuantity!.Value
            })
            .ToListAsync(cancelToken);

        // Multiplied here rather than in the projection, so the product's width is C#'s question and not the provider's.
        Appetite[] appetites = [.. rows.Select(row => new Appetite(row.CampaignMapTileId, row.SetId, (long)row.Individuals * row.Quantity))];

        return Dominant([.. appetites.Where(appetite => appetite.CampaignMapTileId == tileId)]) ?? Dominant(appetites);
    }

    /// <summary>
    /// The set the largest part of a group of appetites belongs to, ties going to the lowest set id so the answer never depends on the read's order.
    /// </summary>
    private static int? Dominant(IReadOnlyCollection<Appetite> appetites)
    {
        return appetites
            .GroupBy(appetite => appetite.FoodSupplySetId)
            .OrderByDescending(group => group.Sum(appetite => appetite.Need))
            .ThenBy(group => group.Key)
            .Select(group => (int?)group.Key)
            .FirstOrDefault();
    }

    /// <summary>
    /// Every patch on the tile the Player has discovered, with what their estimate of it amounts to in each Asset it yields.
    /// </summary>
    /// <remarks>
    /// Query syntax for the joins: none of discovery, instance, type and yield carries a navigation to the next, so these are genuine joins between tables
    /// rather than projections through mapped relationships. The same join is written the same way in <c>GetPlayerMapResourcesHandler</c>.
    /// </remarks>
    private static async Task<IReadOnlyList<EstimatedPatch>> ReadKnownPatchesAsync(
        AscendantEmpireContext context,
        int gameId,
        int playerId,
        int tileId,
        CancellationToken cancelToken)
    {
        var rows = await (
            from known in context.PlayerKnownMapResources.AsNoTracking()
            join instance in context.MapResourceInstances on known.MapResourceInstanceId equals instance.Id
            join type in context.MapResourceTypes on instance.MapResourceTypeId equals type.Id
            join yield in context.MapResourceTypeYields on type.Id equals yield.MapResourceTypeId
            where known.GameId == gameId
                && known.PlayerId == playerId
                && instance.CampaignMapId == tileId
            select new
            {
                InstanceId = instance.Id,
                instance.Slot,
                instance.Quantity,
                instance.EstimationFuzzyFactor,
                TypeId = type.Id,
                type.DisplayName,
                type.IsStratified,
                type.IsDistribution,
                yield.AssetId,
                yield.AmountPerUnit,
                yield.IsEnabledByDefault
            })
            .ToListAsync(cancelToken);

        if (rows.Count == 0)
        {
            return [];
        }

        IReadOnlyDictionary<(int TypeId, int AssetId), bool> switchesByYield = await MapResourceYieldSwitches.ReadAsync(context, gameId, playerId, cancelToken);

        // The same key the estimate is derived from everywhere else, so the toolbar's total is built out of the very figures drawn beside it.
        long gameSeed = await context.GameIndex
            .Where(game => game.Id == gameId)
            .Select(game => game.GenerationSeed)
            .FirstOrDefaultAsync(cancelToken);

        return
        [
            .. rows
                .OrderBy(row => row.Slot)
                .ThenBy(row => row.AssetId)
                .Select(row => new EstimatedPatch(
                    row.InstanceId,
                    row.DisplayName,
                    row.AssetId,
                    EstimatedAmount(gameSeed, playerId, row.InstanceId, row.Quantity, row.EstimationFuzzyFactor, row.IsStratified, row.IsDistribution, row.AmountPerUnit),
                    switchesByYield.TryGetValue((row.TypeId, row.AssetId), out bool isEnabled) ? isEnabled : row.IsEnabledByDefault))
        ];
    }

    /// <summary>
    /// How much of the Asset the Player's estimate of a patch amounts to, or <see langword="null"/> where they have no estimate of it to go on.
    /// </summary>
    /// <remarks>
    /// Null rather than zero for a patch whose size they cannot make out, so it is left out of the total instead of counted as empty. Both figures are raw
    /// fixed-point, so the scale has to come back out of their product.
    /// </remarks>
    private static ScaledAmount? EstimatedAmount(
        long gameSeed,
        int playerId,
        int instanceId,
        long? quantity,
        decimal? estimationFuzzyFactor,
        bool isStratified,
        bool isDistribution,
        long amountPerUnit)
    {
        if (isStratified || isDistribution || quantity is null)
        {
            return null;
        }

        long? estimate = MapResourceEstimationRules.Estimate(gameSeed, instanceId, playerId, quantity.Value, estimationFuzzyFactor);

        return estimate is null ? null : ScaledAmount.FromRaw((long)((Int128)estimate.Value * amountPerUnit / ScaledAmount.Scale));
    }

    /// <summary>
    /// One Force of the Player's that eats, flattened to the tile it stands on, the set it eats from and the rations it needs.
    /// </summary>
    private sealed record Appetite(int CampaignMapTileId, int FoodSupplySetId, long Need);

    /// <summary>
    /// One Asset a discovered patch yields, at the Player's own estimate of how much of it is there.
    /// </summary>
    private sealed record EstimatedPatch(int InstanceId, string DisplayName, int AssetId, ScaledAmount? EstimatedAmount, bool IsYieldEnabled);
}
