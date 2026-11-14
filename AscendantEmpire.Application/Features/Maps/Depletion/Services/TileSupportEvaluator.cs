using AscendantEmpire.Application.Data;
using AscendantEmpire.Application.Features.Maps.Biomes.Classes;
using AscendantEmpire.Application.Features.Maps.Biomes.Services;
using AscendantEmpire.Application.Features.Maps.CampaignMaps.Classes;
using AscendantEmpire.Application.Features.Maps.CampaignMaps.Services;
using AscendantEmpire.Application.Features.Maps.Depletion.Classes;
using AscendantEmpire.Application.Helpers.ScaledNumerics;
using AscendantEmpire.BackendContracts.CampaignMaps;
using AscendantEmpire.BackendContracts.CampaignMaps.GetPlayerCampaignMap;
using AscendantEmpire.Domain.Entities.Games;
using AscendantEmpire.Domain.Enums.Maps;

using Microsoft.EntityFrameworkCore;

using static AscendantEmpire.Application.Common.ServiceResultBase;

namespace AscendantEmpire.Application.Features.Maps.Depletion.Services;

/// <summary>
/// Works out tiles' support levels from the Player's Forces, their food values and each tile's depletion, in a fixed number of queries however many tiles are asked about.
/// </summary>
/// <remarks>
/// <para>
/// Per tile, the need is what the Player's own Forces standing on the tile eat, together with the requested Forces, each Force counted once - so a requested Force already
/// on the tile is not doubled. A Force's appetite is its individuals (healthy and injured alike) times the quantity its design's body states, compiled onto its template.
/// The level and the number of Turns come from <see cref="TileSupportForecaster"/>, which feeds from the same sources as the end-of-Turn feed and regrows the tile as the
/// end of the Round does.
/// </para>
/// <para>
/// Where the Forces on a tile eat from different food sets - which no seeded design does, there being one Food set - the tile is forecast against the set the largest part
/// of the need belongs to. The indicator is an estimate, and the alternative is a forecast per set that no tile could honour anyway, the Forces sharing one tile.
/// </para>
/// <para>
/// Forces count exactly as they do in <c>Gathering</c>: a wiped-out Force has nobody to feed, and a Force in a Battle is fighting rather than foraging. A requested Force of
/// the Player's that is in a Battle is therefore not an error - it simply adds nothing.
/// </para>
/// <para>
/// Troop counts are always read here, never taken from the client.
/// </para>
/// </remarks>
internal class TileSupportEvaluator(
    IPlayerSustenanceValueReader sustenanceValueReader,
    IBiomeRegenerationValueReader biomeRegenerationValueReader,
    ISustenanceAssetReader sustenanceAssetReader,
    ISupplySetMembershipReader supplySetMembershipReader,
    ITileFoodStockReader tileFoodStockReader,
    ICampaignRiverReader campaignRiverReader) : ITileSupportEvaluator
{
    public async Task<ServiceResult<IReadOnlyDictionary<int, TileSupport?>>> EvaluateAsync(
        AscendantEmpireContext context,
        int gameId,
        int playerId,
        IReadOnlyCollection<SightTerrain> tiles,
        CampaignMapGeometry geometry,
        IReadOnlyCollection<int> forceIds,
        CancellationToken cancelToken = default)
    {
        // Every Force of the Player's in the Game, feeding or not. A Player has few enough Forces that reading them all once is cheaper than a query per purpose, and it
        // lets the requested ids be checked for ownership against the same rows. Scalars only: nothing here needs the compiled template.
        var playerForces = await context.UnitInstances
            .AsNoTracking()
            .Where(u => u.GameId == gameId)
            .Where(u => u.PlayerId == playerId)
            .Select(u => new
            {
                u.Id,
                u.CampaignMapTileId,
                u.BattleMapTileId,
                Individuals = (long)(u.HealthyCount + u.InjuredCount),
                FoodSupplySetId = u.UnitTemplate.Compiled!.FoodSupplySetId,
                FoodQuantity = u.UnitTemplate.Compiled!.FoodQuantity
            })
            .ToListAsync(cancelToken);

        // A requested id the Player does not own - someone else's Force, or no Force at all - is refused rather than quietly ignored, so a client asking about Forces it
        // should not know the size of is told no.
        var playerForceIds = playerForces.Select(f => f.Id).ToHashSet();
        int[] unknownForceIds = [.. forceIds.Where(id => playerForceIds.Contains(id) == false).Distinct().Order()];

        if (unknownForceIds.Length > 0)
        {
            return ServiceResult<IReadOnlyDictionary<int, TileSupport?>>.Fail(
                $"Force Id(s) {string.Join(", ", unknownForceIds)} are not Forces of Player Id {playerId} in Game Id {gameId}.");
        }

        // Only the Forces that eat, as Gathering counts them: some individuals left, not in a Battle, and a design that states a food need at all.
        var feedingForces = playerForces
            .Where(f => f.Individuals > 0)
            .Where(f => f.BattleMapTileId == null)
            .Where(f => f.FoodSupplySetId is not null && f.FoodQuantity is > 0)
            .Select(f => new ForceAppetite(f.Id, f.CampaignMapTileId, f.FoodSupplySetId!.Value, ScaledAmount.FromRaw(f.Individuals * f.FoodQuantity!.Value)))
            .ToList();

        var requestedForceIdSet = forceIds.ToHashSet();

        // The requested Forces are counted on every tile. Kept by id, so a tile's own Forces can be unioned with them without counting any Force twice.
        Dictionary<int, ForceAppetite> requestedAppetitesByForceId = feedingForces
            .Where(f => requestedForceIdSet.Contains(f.UnitInstanceId))
            .ToDictionary(f => f.UnitInstanceId);

        ILookup<int, ForceAppetite> feedingForcesByTileId = feedingForces.ToLookup(f => f.CampaignMapTileId);

        int[] tileIds = [.. tiles.Select(t => t.CampaignMapId).Distinct()];

        // What has been taken from each tile, by anyone. A tile never fed on has no row, which TileFoodPool reads as nothing taken.
        Dictionary<int, CampaignMapDepletion> depletionByTileId = await context.CampaignMapDepletions
            .AsNoTracking()
            .Where(d => tileIds.Contains(d.CampaignMapId))
            .ToDictionaryAsync(d => d.CampaignMapId, cancelToken);

        PlayerSustenanceValues values = await sustenanceValueReader.GetValuesAsync(context, playerId, cancelToken);

        // What each biome regrows at the end of a Round, so the forecast can count the Turns regrowth adds.
        Dictionary<BiomeType, BiomeRegenerationValues> regenerationByBiome = await biomeRegenerationValueReader.GetValuesAsync(context, cancelToken);

        // The tiles' river edges, which raise a riverbank's Hunt and put Fish on it. One call however many tiles: the reader loads the North and West neighbours that own
        // the other two edges of each.
        Coord[] coords = [.. tiles.Select(tile => new Coord(tile.X, tile.Y)).DistinctBy(coord => (coord.X, coord.Y))];
        IReadOnlyDictionary<Coord, TileRivers> riversByCoord = await campaignRiverReader.GetTileRiversAsync(context, gameId, coords, geometry, cancelToken);

        // What each biome resource is as an Asset, what each food set prices its Assets at, and what the patches on each tile hold - the three things that
        // turn a biome value into something a particular design can eat.
        IReadOnlyDictionary<SustenanceResource, int> sustenanceAssetIds = await sustenanceAssetReader.GetAssetIdsAsync(context, cancelToken);

        IReadOnlyDictionary<int, IReadOnlyDictionary<int, long>> membershipsBySetId = await supplySetMembershipReader.GetAmountsPerUnitAsync(
            context,
            [.. feedingForces.Select(f => f.FoodSupplySetId).Distinct()],
            cancelToken);

        IReadOnlyDictionary<int, IReadOnlyCollection<TileMapResourceStock>> stocksByTileId = await tileFoodStockReader.GetStocksAsync(
            context,
            gameId,
            playerId,
            tileIds,
            cancelToken);

        var supportByTileId = new Dictionary<int, TileSupport?>();

        foreach (SightTerrain tile in tiles)
        {
            // Several Observers can see the same tile; it only needs evaluating once.
            if (supportByTileId.ContainsKey(tile.CampaignMapId))
            {
                continue;
            }

            // The tile's own Forces, then the requested ones not already among them.
            var appetitesByForceId = new Dictionary<int, ForceAppetite>(requestedAppetitesByForceId);

            foreach (ForceAppetite appetite in feedingForcesByTileId[tile.CampaignMapId])
            {
                appetitesByForceId[appetite.UnitInstanceId] = appetite;
            }

            ScaledAmount need = appetitesByForceId.Values.Aggregate(ScaledAmount.Zero, (sum, appetite) => sum + appetite.Need);

            depletionByTileId.TryGetValue(tile.CampaignMapId, out CampaignMapDepletion? depletion);

            TileSupport? support = TileSupportForecaster.Forecast(
                need,
                CampaignTileTerrain.IsWater(tile.BiomeIsWater, tile.GeographyIsWater),
                values,
                tile.BiomeType,
                depletion,
                regenerationByBiome.GetValueOrDefault(tile.BiomeType),
                sustenanceAssetIds,
                MembershipOfLargestAppetite(appetitesByForceId.Values, membershipsBySetId) ?? StandardRations(sustenanceAssetIds, stocksByTileId.GetValueOrDefault(tile.CampaignMapId, [])),
                stocksByTileId.GetValueOrDefault(tile.CampaignMapId, []),
                riversByCoord.GetValueOrDefault(new Coord(tile.X, tile.Y)));

            supportByTileId.Add(tile.CampaignMapId, support);
        }

        return ServiceResult<IReadOnlyDictionary<int, TileSupport?>>.Succeed(supportByTileId);
    }

    /// <summary>
    /// The prices of the food set the largest part of a tile's need belongs to, which is every Force's set while only one is seeded.
    /// </summary>
    /// <remarks>
    /// Ties go to the lowest set id, so a tile's forecast depends only on what stands on it and not on the order the Forces came back in.
    /// </remarks>
    /// <returns><see langword="null"/> when nothing is going to eat on the tile, there being no appetite to take a set from.</returns>
    private static IReadOnlyDictionary<int, long>? MembershipOfLargestAppetite(
        IReadOnlyCollection<ForceAppetite> appetites,
        IReadOnlyDictionary<int, IReadOnlyDictionary<int, long>> membershipsBySetId)
    {
        int? dominantSetId = appetites
            .GroupBy(appetite => appetite.FoodSupplySetId)
            .OrderByDescending(group => group.Aggregate(ScaledAmount.Zero, (sum, appetite) => sum + appetite.Need).RawValue)
            .ThenBy(group => group.Key)
            .Select(group => (int?)group.Key)
            .FirstOrDefault();

        return dominantSetId is null ? null : membershipsBySetId.GetValueOrDefault(dominantSetId.Value, new Dictionary<int, long>());
    }

    /// <summary>
    /// Everything on a tile priced at one ration apiece, for a tile with nobody to feed.
    /// </summary>
    /// <remarks>
    /// With no Force there is no set to price against, and pricing nothing would read an untouched tile as stripped. The figures never reach a forecast - a tile with no
    /// need returns as soon as it is found not to be exhausted - so all this decides is whether the tile has anything on it at all.
    /// </remarks>
    private static IReadOnlyDictionary<int, long> StandardRations(
        IReadOnlyDictionary<SustenanceResource, int> sustenanceAssetIds,
        IReadOnlyCollection<TileMapResourceStock> stocks)
    {
        return sustenanceAssetIds.Values
            .Concat(stocks.Select(stock => stock.AssetId))
            .Distinct()
            .ToDictionary(assetId => assetId, _ => ScaledAmount.Scale);
    }

    /// <summary>
    /// One Force counted against a tile: what set it eats from, and how many rations it needs each Turn.
    /// </summary>
    private sealed record ForceAppetite(int UnitInstanceId, int CampaignMapTileId, int FoodSupplySetId, ScaledAmount Need);
}
