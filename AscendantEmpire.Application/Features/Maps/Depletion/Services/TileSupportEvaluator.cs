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
/// Per tile, the need is the troops (healthy and injured, 1 troop = 1 food) of the Player's own Forces standing on the tile together with the requested Forces, each Force
/// counted once - so a requested Force already on the tile is not doubled. The level and the number of Turns come from <see cref="TileSupportForecaster"/>, which feeds
/// from the same pool as the end-of-Turn feed and regrows the tile as the end of the Round does.
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
            .Select(u => new { u.Id, u.CampaignMapTileId, u.BattleMapTileId, Troops = u.HealthyCount + u.InjuredCount })
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

        // Only the Forces that eat, as Gathering counts them: some troops left, and not in a Battle.
        var feedingForces = playerForces
            .Where(f => f.Troops > 0)
            .Where(f => f.BattleMapTileId == null)
            .ToList();

        var requestedForceIdSet = forceIds.ToHashSet();

        // The requested Forces are counted on every tile. Kept by id, so a tile's own Forces can be unioned with them without counting any Force twice.
        Dictionary<int, long> requestedTroopsByForceId = feedingForces
            .Where(f => requestedForceIdSet.Contains(f.Id))
            .ToDictionary(f => f.Id, f => (long)f.Troops);

        ILookup<int, (int Id, long Troops)> feedingForcesByTileId = feedingForces.ToLookup(f => f.CampaignMapTileId, f => (f.Id, (long)f.Troops));

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

        var supportByTileId = new Dictionary<int, TileSupport?>();

        foreach (SightTerrain tile in tiles)
        {
            // Several Observers can see the same tile; it only needs evaluating once.
            if (supportByTileId.ContainsKey(tile.CampaignMapId))
            {
                continue;
            }

            // The tile's own Forces, then the requested ones not already among them.
            var troopsByForceId = new Dictionary<int, long>(requestedTroopsByForceId);

            foreach ((int forceId, long troops) in feedingForcesByTileId[tile.CampaignMapId])
            {
                troopsByForceId[forceId] = troops;
            }

            // 1 troop needs 1 food.
            ScaledAmount need = ScaledAmount.FromWhole(troopsByForceId.Values.Sum());

            depletionByTileId.TryGetValue(tile.CampaignMapId, out CampaignMapDepletion? depletion);

            TileSupport? support = TileSupportForecaster.Forecast(
                need,
                CampaignTileTerrain.IsWater(tile.BiomeIsWater, tile.GeographyIsWater),
                values,
                tile.BiomeType,
                depletion,
                regenerationByBiome.GetValueOrDefault(tile.BiomeType),
                riversByCoord.GetValueOrDefault(new Coord(tile.X, tile.Y)));

            supportByTileId.Add(tile.CampaignMapId, support);
        }

        return ServiceResult<IReadOnlyDictionary<int, TileSupport?>>.Succeed(supportByTileId);
    }
}
