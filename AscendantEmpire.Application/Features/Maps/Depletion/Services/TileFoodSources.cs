using AscendantEmpire.Application.Features.Maps.Biomes.Classes;
using AscendantEmpire.Application.Features.Maps.Biomes.Services;
using AscendantEmpire.Application.Features.Maps.Depletion.Classes;
using AscendantEmpire.Application.Helpers.ScaledNumerics;
using AscendantEmpire.BackendContracts.CampaignMaps.GetPlayerCampaignMap;
using AscendantEmpire.Domain.Entities.Games;
using AscendantEmpire.Domain.Enums.Maps;

namespace AscendantEmpire.Application.Features.Maps.Depletion.Services;

/// <summary>
/// The food a campaign map tile offers a Player's Forces: one source per thing on the tile they can eat, named as the Asset it is.
/// </summary>
/// <remarks>
/// <para>
/// The one place the sources are built, shared by the end-of-Turn feed (<c>Gathering</c>) and the support indicator (<see cref="ITileSupportEvaluator"/>).
/// </para>
/// <para>
/// A tile offers two kinds of thing. Its biome gives a rate that regrows, which is what the <c>*Taken</c> columns are measured against; a MapResource patch
/// standing on it gives a stock that does not. Both are Assets, and what decides whether either may be eaten is the same thing: membership of the
/// <c>SupplySet</c> the eating Force's body requires.
/// </para>
/// </remarks>
internal static class TileFoodSources
{
    /// <summary>
    /// What a dry land tile's biome offers. Fodder is for animals, and nothing eats it yet.
    /// </summary>
    private static readonly SustenanceResource[] _LAND_RESOURCES = [SustenanceResource.Forage, SustenanceResource.Hunt];

    /// <summary>
    /// What a land tile with a river on one of its edges offers: the dry resources, plus the Fish the river puts there.
    /// </summary>
    private static readonly SustenanceResource[] _RIVER_LAND_RESOURCES = [SustenanceResource.Forage, SustenanceResource.Hunt, SustenanceResource.Fish];

    /// <summary>
    /// What a water tile's biome offers.
    /// </summary>
    private static readonly SustenanceResource[] _WATER_RESOURCES = [SustenanceResource.Fish];

    /// <summary>
    /// Every source of food the tile offers the Player, biome and MapResources alike.
    /// </summary>
    /// <param name="values">The Player's effective sustenance values.</param>
    /// <param name="biome">The tile's biome.</param>
    /// <param name="depletion">The tile's depletion row, or <see langword="null"/> when it has never been fed on.</param>
    /// <param name="sustenanceAssetIds">The Asset each biome resource is identified as, from <see cref="SustenanceResourceAssets"/>.</param>
    /// <param name="amountPerUnitByAssetId">
    /// What one unit of the need costs in each Asset the required <c>SupplySet</c> holds. An Asset absent from it is not food, whether it came from the
    /// biome or from a patch.
    /// </param>
    /// <param name="mapResourceStocks">The MapResource instances standing on the tile, as the caller read them.</param>
    /// <param name="rivers">The tile's four river edges, or <see langword="null"/> when they are not known, which is read as no rivers.</param>
    /// <returns>
    /// One entry per source. A biome resource the tile does not have at all (a null effective value) is left out - unlike a stripped one, which is offered
    /// at 0, because it is still a thing the tile grows.
    /// </returns>
    public static IReadOnlyCollection<FoodSource> GetAvailable(
        bool isWater,
        PlayerSustenanceValues values,
        BiomeType biome,
        CampaignMapDepletion? depletion,
        IReadOnlyDictionary<SustenanceResource, int> sustenanceAssetIds,
        IReadOnlyDictionary<int, long> amountPerUnitByAssetId,
        IReadOnlyCollection<TileMapResourceStock> mapResourceStocks,
        TileRivers? rivers = null)
    {
        var sources = new List<FoodSource>();

        foreach (SustenanceResource resource in GetBiomeResources(isWater, rivers))
        {
            if (sustenanceAssetIds.TryGetValue(resource, out int assetId) == false
                || amountPerUnitByAssetId.TryGetValue(assetId, out long amountPerUnit) == false)
            {
                continue;
            }

            // The rivers are applied to the Player's effective value, before anything taken is removed: a river raises what the tile holds, not what is left of it.
            ScaledAmount? effectiveValue = RiverSustenanceBonus.Apply(values.GetValue(biome, resource), resource, rivers, isWater);

            ScaledAmount? availableValue = SustenanceValueCalculator.CalculateAvailableValue(effectiveValue, DepletionTaken.GetTaken(depletion, resource));

            if (availableValue is not null)
            {
                sources.Add(new FoodSource(
                    new FoodSourceKey(FoodSourceType.Tile, resource, MapResourceInstanceId: null, assetId),
                    availableValue.Value,
                    amountPerUnit));
            }
        }

        foreach (TileMapResourceStock stock in mapResourceStocks)
        {
            if (stock.IsYieldEnabled == false
                || stock.Available is null
                || amountPerUnitByAssetId.TryGetValue(stock.AssetId, out long amountPerUnit) == false)
            {
                continue;
            }

            sources.Add(new FoodSource(
                new FoodSourceKey(FoodSourceType.MapResource, Resource: null, stock.MapResourceInstanceId, stock.AssetId),
                stock.Available.Value,
                amountPerUnit));
        }

        return sources;
    }

    /// <summary>
    /// Which of the biome's resources the tile offers.
    /// </summary>
    /// <remarks>
    /// Fish is only on a land tile where a river puts it there. A land biome carrying its own Fish value - which the biome editor does not allow - is left
    /// out as it always has been.
    /// </remarks>
    private static SustenanceResource[] GetBiomeResources(bool isWater, TileRivers? rivers)
    {
        if (isWater)
        {
            return _WATER_RESOURCES;
        }

        return rivers?.HasAny == true ? _RIVER_LAND_RESOURCES : _LAND_RESOURCES;
    }
}
