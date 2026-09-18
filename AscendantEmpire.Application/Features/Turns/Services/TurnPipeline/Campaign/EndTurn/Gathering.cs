using AscendantEmpire.Application.Data;
using AscendantEmpire.Application.Features.Maps.Biomes.Classes;
using AscendantEmpire.Application.Features.Maps.Biomes.Services;
using AscendantEmpire.Application.Features.Maps.CampaignMaps.Services;
using AscendantEmpire.Application.Features.Maps.Depletion.Classes;
using AscendantEmpire.Application.Features.Maps.Depletion.Services;
using AscendantEmpire.Application.Features.Units.Classes;
using AscendantEmpire.Application.Features.Units.Services;
using AscendantEmpire.Application.Helpers.ScaledNumerics;
using AscendantEmpire.Domain.Entities.Games;
using AscendantEmpire.Domain.Enums.Resources;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace AscendantEmpire.Application.Features.Turns.Services.TurnPipeline.Campaign.EndTurn;

/// <summary>
/// Feeds each of the Player's Forces from the campaign map tile it ends the Turn on, adding what they eat to the tile's Player-neutral <c>Taken</c> amounts.
/// </summary>
/// <remarks>
/// <para>
/// Per tile, the Forces on it feed from one pool: Forage and Hunt on land, Fish on water (fodder is for animals, and nothing eats it yet), built by <see cref="TileFoodPool"/>.
/// 1 troop needs 1 food. What is available of each resource to this Player is <c>MAX(0, effective − Taken)</c>, with the effective values read live through
/// <see cref="IPlayerSustenanceValueReader"/>.
/// The need is split across the pool by <see cref="FoodExtractionCalculator"/>, and any shortfall is spread across the tile's Forces by <see cref="ShortfallAllocator"/>
/// and handed to <see cref="IUnderfedForceResolver"/>. See "What a tile stores: the amount taken" in <c>docs/design/campaign-map.md</c>.
/// </para>
/// <para>
/// A tile fed on is marked with <see cref="CampaignMapDepletion.FedOnRound"/> whether or not there was anything left to take: the marker records presence, not food.
/// </para>
/// <para>
/// Forces in a Battle do not feed. That also delivers "only one side feeds from a tile": opposing Forces can only share a tile by being in a Battle on it.
/// </para>
/// <para>
/// <b>Reruns.</b> Nothing records that a Player's Forces have already fed this Turn, so running this twice for one Turn feeds them twice. The turn pipeline is not
/// transactional, so a retry after a later process fails would do exactly that - see the TODO below.
/// </para>
/// </remarks>
internal class Gathering(
    IDbContextFactory<AscendantEmpireContext> dbFactory,
    IPlayerSustenanceValueReader sustenanceValueReader,
    IUnderfedForceResolver underfedForceResolver,
    ILogger<Gathering> logger) : IEndCampaignTurnProcess
{
    public string ItemName => "Gathering";

    public int ExecutionOrder => (int)EndCampaignTurnProcessOrder.Gathering;

    public async Task<string> ProcessAsync(TurnPipelineContextData context)
    {
        // TODO: Passing through a tile should draw 20% of a Force's need from it, and mark it fed on (FedOnRound) as well. The tiles passed through in the Turn
        //       are in units.MovementHistory; the draw needs its own ordering rules against the end-of-Turn feed.
        // TODO: UNIT_HARVEST_CAP - a maximum a Force can harvest from a tile regardless of availability (and pillaging to exceed it). See docs/design/campaign-map.md.
        // TODO: Food needs should come from each Force's unit design rather than 1 troop = 1 food, and mounted units should need Fodder as well.
        // TODO: Force gathering bonuses (a forager attached to a Command) should reduce what a Force draws from the tile.
        // TODO: A hostile Force on a Settlement's tile takes the tile's food and the Settlement gets none. Waits on Settlements being modelled.
        // TODO: A hidden Force feeding here raises the tile's shared Taken, which lowers what every watching Player sees (toolbar values, support level, Turns forecast) and so
        //       signals it is there. Going hungry must not give it away either. Waits on hiding and detection being modelled - see CM-45.
        // TODO: Rerun risk. The pipeline is not transactional, so if a later end-of-Turn process fails and the Turn is ended again, these Forces feed a second time.
        //       Guard it (a per-Force or per-Turn fed stamp) once the pipeline's retry story is settled.
        // TODO: Open design questions - should tiles controlled by a Settlement (within its boundaries) be exempt? And fortification work leaves a Force on one tile for
        //       many Turns, stripping it; must such Forces always be supplied, and is that a bad thing?

        if (context.PlayerId is null)
        {
            // The end of a Turn is always Player-scoped. A null here is a defect in the caller, and feeding every Player's Forces in the Game would be a great deal
            // worse than refusing.
            string error = $"{ItemName} was given an end-of-Turn context with no Player Id for Game Id {context.GameId}.";
            logger.LogError("{Error}", error);

            return error;
        }

        int playerId = context.PlayerId.Value;

        await using AscendantEmpireContext db = await dbFactory.CreateDbContextAsync();

        // Scalars only: nothing here needs the Force entity, and materialising it would drag the compiled template along for no reason.
        //
        // Wiped-out Forces have no one to feed. Forces on a Battle Map are fighting rather than foraging, and are the only way two sides can share a tile.
        var forces = await db.UnitInstances
            .Where(u => u.GameId == context.GameId)
            .Where(u => u.PlayerId == playerId)
            .Where(u => u.HealthyCount + u.InjuredCount > 0)
            .Where(u => u.BattleMapTileId == null)
            .Select(u => new { u.Id, u.CampaignMapTileId, Troops = u.HealthyCount + u.InjuredCount })
            .ToListAsync();

        if (forces.Count == 0)
        {
            return string.Empty;
        }

        int[] tileIds = [.. forces.Select(f => f.CampaignMapTileId).Distinct()];

        // What the pool on each tile is made of depends on whether it is water, which the biome and geography decide between them.
        var tiles = await db.CampaignMaps
            .Where(t => tileIds.Contains(t.CampaignMapId))
            .Select(t => new { t.CampaignMapId, t.BiomeTypeId, BiomeIsWater = t.Biome.IsWater, t.CampaignMapGeographyTypeId })
            .ToDictionaryAsync(t => t.CampaignMapId);

        // Tracked, because these rows are about to be written. A tile never fed on has no row yet, and is given one below.
        Dictionary<int, CampaignMapDepletion> depletionByTileId = await db.CampaignMapDepletions
            .Where(d => tileIds.Contains(d.CampaignMapId))
            .ToDictionaryAsync(d => d.CampaignMapId);

        // Read through this context, so the values are the Player's as of this pipeline run.
        PlayerSustenanceValues values = await sustenanceValueReader.GetValuesAsync(db, playerId);

        var results = new List<ForceFeedingResult>();
        ScaledAmount totalFed = ScaledAmount.Zero;
        ScaledAmount totalShortfall = ScaledAmount.Zero;

        foreach (var forcesOnTile in forces.GroupBy(f => f.CampaignMapTileId).OrderBy(g => g.Key))
        {
            int tileId = forcesOnTile.Key;

            if (tiles.TryGetValue(tileId, out var tile) == false)
            {
                // A Force pointing at a tile that does not exist is a data defect, but failing here would leave the Player unable to end their Turn at all.
                logger.LogWarning(
                    "Campaign map tile Id {CampaignMapTileId} was not found while feeding Player Id {PlayerId}'s Forces in Game Id {GameId}, so the Forces on it did not feed.",
                    tileId,
                    playerId,
                    context.GameId);

                continue;
            }

            if (depletionByTileId.TryGetValue(tileId, out CampaignMapDepletion? depletion) == false)
            {
                depletion = new CampaignMapDepletion { CampaignMapId = tileId, GameId = context.GameId };
                db.CampaignMapDepletions.Add(depletion);
                depletionByTileId.Add(tileId, depletion);
            }

            // 1 troop needs 1 food.
            Dictionary<int, long> troopsByForceId = forcesOnTile.ToDictionary(f => f.Id, f => (long)f.Troops);
            ScaledAmount need = ScaledAmount.FromWhole(troopsByForceId.Values.Sum());

            // The pool: what is available of each resource this tile offers, to this Player. Built by TileFoodPool, which the support indicator shares, so the colour a
            // tile is shown in always comes from the same pool the Forces eat from here.
            Dictionary<SustenanceResource, ScaledAmount> available = TileFoodPool.GetAvailable(
                CampaignTileWater.IsWater(tile.BiomeIsWater, tile.CampaignMapGeographyTypeId),
                values,
                tile.BiomeTypeId,
                depletion);

            FoodExtraction extraction = FoodExtractionCalculator.Extract(need, available);

            foreach ((SustenanceResource resource, ScaledAmount taken) in extraction.Taken)
            {
                DepletionTaken.AddTaken(depletion, resource, taken);
            }

            // Set whether or not anything was taken: the marker records that Forces were here.
            depletion.FedOnRound = context.CurrentRoundNumber;

            Dictionary<int, ScaledAmount> shortfallByForceId = ShortfallAllocator.Allocate(extraction.Shortfall, troopsByForceId);

            foreach ((int forceId, long troops) in troopsByForceId.OrderBy(entry => entry.Key))
            {
                ScaledAmount forceNeed = ScaledAmount.FromWhole(troops);
                ScaledAmount forceShortfall = shortfallByForceId[forceId];

                results.Add(new ForceFeedingResult(forceId, tileId, forceNeed, forceNeed - forceShortfall, forceShortfall));
            }

            totalFed += extraction.TotalTaken;
            totalShortfall += extraction.Shortfall;
        }

        // Before saving, so whatever the consequences of going hungry write lands in the same save as the depletion.
        await underfedForceResolver.ResolveAsync(db, context.GameId, playerId, results);

        await db.SaveChangesAsync();

        logger.LogInformation(
            "Fed {ForceCount} Force(s) belonging to Player Id {PlayerId} in Game Id {GameId}, Round {RoundNumber}, across {TileCount} tile(s): {TotalFed} food taken, {TotalShortfall} short.",
            results.Count,
            playerId,
            context.GameId,
            context.CurrentRoundNumber,
            tileIds.Length,
            totalFed.ToDecimal(),
            totalShortfall.ToDecimal());

        return string.Empty;
    }
}
