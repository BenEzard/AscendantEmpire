using AscendantEmpire.Application.Data;
using AscendantEmpire.Application.Features.Maps.Biomes.Classes;
using AscendantEmpire.Application.Features.Maps.Biomes.Services;
using AscendantEmpire.Application.Features.Maps.CampaignMaps.Classes;
using AscendantEmpire.Application.Features.Maps.CampaignMaps.Services;
using AscendantEmpire.Application.Helpers.ScaledNumerics;
using AscendantEmpire.Domain.Entities.Games;
using AscendantEmpire.Domain.Enums.Resources;

using Microsoft.EntityFrameworkCore;

using static AscendantEmpire.Application.Common.ServiceResultBase;

namespace AscendantEmpire.Application.Features.Maps.Depletion.Services;

/// <summary>
/// Works out tiles' support levels from the Player's Forces, their food values and each tile's depletion, in a fixed number of queries however many tiles are asked about.
/// </summary>
/// <remarks>
/// <para>
/// Per tile, the need is the troops (healthy and injured, 1 troop = 1 food) of the Player's own Forces standing on the tile together with the requested Forces, each Force
/// counted once - so a requested Force already on the tile is not doubled. The pool comes from <see cref="TileFoodPool"/> and the level from
/// <see cref="ForceSupportCalculator"/>, the same pool the end-of-Turn feed eats from.
/// </para>
/// <para>
/// Forces count exactly as they do in <c>Gathering</c>: a wiped-out Force has nobody to feed, and a Force in a Battle is fighting rather than foraging. A requested Force of
/// the Player's that is in a Battle is therefore not an error - it simply adds nothing.
/// </para>
/// <para>
/// Troop counts are always read here, never taken from the client.
/// </para>
/// </remarks>
internal class TileSupportEvaluator(IPlayerSustenanceValueReader sustenanceValueReader) : ITileSupportEvaluator
{
    public async Task<ServiceResult<IReadOnlyDictionary<int, TileSupportLevel?>>> EvaluateAsync(
        AscendantEmpireContext context,
        int gameId,
        int playerId,
        IReadOnlyCollection<SightTerrain> tiles,
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
            return ServiceResult<IReadOnlyDictionary<int, TileSupportLevel?>>.Fail(
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

        var levelByTileId = new Dictionary<int, TileSupportLevel?>();

        foreach (SightTerrain tile in tiles)
        {
            // Several Observers can see the same tile; it only needs evaluating once.
            if (levelByTileId.ContainsKey(tile.CampaignMapId))
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

            Dictionary<SustenanceResource, ScaledAmount> available = TileFoodPool.GetAvailable(
                CampaignTileWater.IsWater(tile.BiomeIsWater, tile.GeographyType),
                values,
                tile.BiomeType,
                depletion);

            ScaledAmount totalAvailable = available.Values.Aggregate(ScaledAmount.Zero, (sum, amount) => sum + amount);

            levelByTileId.Add(tile.CampaignMapId, ForceSupportCalculator.Classify(need, totalAvailable));
        }

        return ServiceResult<IReadOnlyDictionary<int, TileSupportLevel?>>.Succeed(levelByTileId);
    }
}
