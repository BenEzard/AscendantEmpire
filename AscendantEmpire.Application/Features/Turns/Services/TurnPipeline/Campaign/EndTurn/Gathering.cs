using AscendantEmpire.Application.Data;
using AscendantEmpire.Application.Features.Maps.Biomes.Classes;
using AscendantEmpire.Application.Features.Maps.Biomes.Services;
using AscendantEmpire.Application.Features.Maps.CampaignMaps.Classes;
using AscendantEmpire.Application.Features.Maps.CampaignMaps.Services;
using AscendantEmpire.Application.Features.Maps.Depletion.Classes;
using AscendantEmpire.Application.Features.Maps.Depletion.Services;
using AscendantEmpire.Application.Features.Units.Classes;
using AscendantEmpire.Application.Features.Units.Services;
using AscendantEmpire.Application.Helpers.ScaledNumerics;
using AscendantEmpire.BackendContracts.CampaignMaps;
using AscendantEmpire.BackendContracts.CampaignMaps.GetPlayerCampaignMap;
using AscendantEmpire.Domain.Entities.Games;
using AscendantEmpire.Domain.Enums.Maps;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace AscendantEmpire.Application.Features.Turns.Services.TurnPipeline.Campaign.EndTurn;

/// <summary>
/// Feeds each of the Player's Forces from the campaign map tile it ends the Turn on, spending what they eat off the tile's biome and off any patch standing
/// on it.
/// </summary>
/// <remarks>
/// <para>
/// What a Force needs comes from its design: the body's food <c>SupplySet</c> and the quantity of it one individual costs, compiled onto the template. What
/// the tile offers is everything whose Asset that set holds - its biome's own Forage, Hunt and Fish, and the MapResource patches on it - with each Asset
/// priced by the set, so a tile of berries feeds fewer individuals than a tile of game. The sources are built by <see cref="TileFoodSources"/>, split by
/// <see cref="FoodExtractionCalculator"/> and spent by <see cref="IFoodConsumptionService"/>; any shortfall is spread across the tile's Forces by
/// <see cref="ShortfallAllocator"/> and handed to <see cref="IUnderfedForceResolver"/>. See "What a tile stores: the amount taken" in
/// <c>docs/design/campaign-map/depletion.md</c>.
/// </para>
/// <para>
/// Forces on one tile are fed by food set, cheapest set id first. They share a tile rather than a ration, so a group feeding after another sees what the
/// first one took. Today only one Food set is seeded, so the grouping only ever has one group in it.
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
    ISustenanceAssetReader sustenanceAssetReader,
    ISupplySetMembershipReader supplySetMembershipReader,
    ITileFoodStockReader tileFoodStockReader,
    IFoodConsumptionService foodConsumptionService,
    IUnderfedForceResolver underfedForceResolver,
    ISightDataReader sightDataReader,
    ICampaignRiverReader campaignRiverReader,
    ILogger<Gathering> logger) : IEndCampaignTurnProcess
{
    public string ItemName => "Gathering";

    public int ExecutionOrder => (int)EndCampaignTurnProcessOrder.Gathering;

    public async Task<string> ProcessAsync(TurnPipelineContextData context)
    {
        // TODO: Passing through a tile should draw 20% of a Force's need from it, and mark it fed on (FedOnRound) as well. The tiles passed through in the Turn
        //       are in units.MovementHistory; the draw needs its own ordering rules against the end-of-Turn feed.
        // TODO: UNIT_HARVEST_CAP - a maximum a Force can harvest from a tile regardless of availability (and pillaging to exceed it). See docs/design/campaign-map/depletion.md.
        // TODO: A mounted body burns a Fuel set (fodder) per movement point, which nothing spends yet. Its food need is already compiled and fed here.
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

        // Scalars only: nothing here needs the Force entity, and materialising it would drag the compiled template along for no reason. The appetite comes
        // off that template, which is projected through rather than loaded.
        //
        // Wiped-out Forces have no one to feed. Forces on a Battle Map are fighting rather than foraging, and are the only way two sides can share a tile.
        var forces = await db.UnitInstances
            .Where(u => u.GameId == context.GameId)
            .Where(u => u.PlayerId == playerId)
            .Where(u => u.HealthyCount + u.InjuredCount > 0)
            .Where(u => u.BattleMapTileId == null)
            .Select(u => new
            {
                u.Id,
                u.CampaignMapTileId,
                Individuals = (long)(u.HealthyCount + u.InjuredCount),
                FoodSupplySetId = u.UnitTemplate.Compiled!.FoodSupplySetId,
                FoodQuantity = u.UnitTemplate.Compiled!.FoodQuantity
            })
            .ToListAsync();

        // A design that states no food need eats nothing, which is not the same as eating nothing because the tile is bare.
        var feedingForces = forces
            .Where(f => f.FoodSupplySetId is not null && f.FoodQuantity is > 0)
            .Select(f => new FeedingForce(f.Id, f.CampaignMapTileId, f.FoodSupplySetId!.Value, ScaledAmount.FromRaw(f.Individuals * f.FoodQuantity!.Value)))
            .ToList();

        if (feedingForces.Count == 0)
        {
            return string.Empty;
        }

        int[] tileIds = [.. feedingForces.Select(f => f.CampaignMapTileId).Distinct()];

        // What the tile offers depends on whether it is water, which the biome and geography decide between them. The coordinates come back too, because
        // the rivers that enrich a tile are keyed by them rather than by the tile's id.
        var tiles = await db.CampaignMaps
            .Where(t => tileIds.Contains(t.CampaignMapId))
            .Select(t => new { t.CampaignMapId, t.XCoord, t.YCoord, t.BiomeTypeId, BiomeIsWater = t.Biome.IsWater, GeographyIsWater = t.Geography.IsWater })
            .ToDictionaryAsync(t => t.CampaignMapId);

        // The rivers on the tiles being fed from, in one call. A river raises a bank's Hunt and puts Fish on it, so it is part of what the Forces eat from.
        IReadOnlyDictionary<Coord, TileRivers> riversByCoord = await ReadRiversAsync(db, context.GameId, tiles.Values.Select(t => new Coord(t.XCoord, t.YCoord)).ToList());

        // Tracked, because these rows are about to be written. A tile never fed on has no row yet, and is given one below.
        Dictionary<int, CampaignMapDepletion> depletionByTileId = await db.CampaignMapDepletions
            .Where(d => tileIds.Contains(d.CampaignMapId))
            .ToDictionaryAsync(d => d.CampaignMapId);

        // Read through this context, so the values are the Player's as of this pipeline run.
        PlayerSustenanceValues values = await sustenanceValueReader.GetValuesAsync(db, playerId);

        // The three lookups the sources are built from, each read once for the whole feed.
        IReadOnlyDictionary<SustenanceResource, int> sustenanceAssetIds = await sustenanceAssetReader.GetAssetIdsAsync(db);

        IReadOnlyDictionary<int, IReadOnlyDictionary<int, long>> membershipsBySetId = await supplySetMembershipReader.GetAmountsPerUnitAsync(
            db,
            [.. feedingForces.Select(f => f.FoodSupplySetId).Distinct()]);

        IReadOnlyDictionary<int, IReadOnlyCollection<TileMapResourceStock>> stocksByTileId = await tileFoodStockReader.GetStocksAsync(
            db,
            context.GameId,
            playerId,
            tileIds);

        var results = new List<ForceFeedingResult>();
        var consumptions = new List<TileFoodConsumption>();
        ScaledAmount totalFed = ScaledAmount.Zero;
        ScaledAmount totalShortfall = ScaledAmount.Zero;

        foreach (var forcesOnTile in feedingForces.GroupBy(f => f.CampaignMapTileId).OrderBy(g => g.Key))
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

            // What the tile's patches hold, drawn down as each food set's group eats from them.
            IReadOnlyCollection<TileMapResourceStock> stocks = stocksByTileId.GetValueOrDefault(tileId, []);
            var takenOnTile = new Dictionary<FoodSourceKey, ScaledAmount>();

            foreach (var group in forcesOnTile.GroupBy(f => f.FoodSupplySetId).OrderBy(g => g.Key))
            {
                IReadOnlyDictionary<int, long> amountPerUnitByAssetId = membershipsBySetId.GetValueOrDefault(group.Key, new Dictionary<int, long>());

                IReadOnlyCollection<FoodSource> sources = TileFoodSources.GetAvailable(
                    CampaignTileTerrain.IsWater(tile.BiomeIsWater, tile.GeographyIsWater),
                    values,
                    tile.BiomeTypeId,
                    depletion,
                    sustenanceAssetIds,
                    amountPerUnitByAssetId,
                    RemainingStocks(stocks, takenOnTile),
                    riversByCoord.GetValueOrDefault(new Coord(tile.XCoord, tile.YCoord)));

                Dictionary<int, long> needByForceId = group.ToDictionary(f => f.UnitInstanceId, f => f.Need.RawValue);
                ScaledAmount need = group.Aggregate(ScaledAmount.Zero, (sum, force) => sum + force.Need);

                FoodExtraction extraction = FoodExtractionCalculator.Extract(need, sources);

                foreach ((FoodSourceKey key, ScaledAmount taken) in extraction.Taken)
                {
                    takenOnTile[key] = takenOnTile.GetValueOrDefault(key) + taken;
                }

                // A tile's shortfall falls on its Forces in proportion to what each of them needed, which is their individuals where their designs agree.
                Dictionary<int, ScaledAmount> shortfallByForceId = ShortfallAllocator.Allocate(extraction.Shortfall, needByForceId);

                foreach (FeedingForce force in group.OrderBy(f => f.UnitInstanceId))
                {
                    ScaledAmount forceShortfall = shortfallByForceId[force.UnitInstanceId];

                    results.Add(new ForceFeedingResult(force.UnitInstanceId, tileId, force.Need, force.Need - forceShortfall, forceShortfall));
                }

                totalFed += extraction.NeedMet;
                totalShortfall += extraction.Shortfall;
            }

            // Set whether or not anything was taken: the marker records that Forces were here.
            depletion.FedOnRound = context.CurrentRoundNumber;

            consumptions.Add(new TileFoodConsumption(depletion, takenOnTile));
        }

        // Spent before saving, so the tile's loss and the consequences of going hungry land in the same save.
        await foodConsumptionService.ConsumeAsync(db, context.GameId, consumptions);

        // The Round is part of how each Force's casualty roll is derived, and the Turn is what a destroyed Force's history row is stamped with.
        await underfedForceResolver.ResolveAsync(
            db,
            gameId: context.GameId,
            playerId: playerId,
            roundNumber: context.CurrentRoundNumber,
            playerTurnId: context.PlayerTurnId,
            results: results);

        await db.SaveChangesAsync();

        logger.LogInformation(
            "Fed {ForceCount} Force(s) belonging to Player Id {PlayerId} in Game Id {GameId}, Round {RoundNumber}, across {TileCount} tile(s): {TotalFed} ration(s) eaten, {TotalShortfall} short.",
            results.Count,
            playerId,
            context.GameId,
            context.CurrentRoundNumber,
            tileIds.Length,
            totalFed.ToDecimal(),
            totalShortfall.ToDecimal());

        return string.Empty;
    }

    /// <summary>
    /// What each patch on the tile has left after the groups that have already eaten.
    /// </summary>
    private static IReadOnlyCollection<TileMapResourceStock> RemainingStocks(
        IReadOnlyCollection<TileMapResourceStock> stocks,
        IReadOnlyDictionary<FoodSourceKey, ScaledAmount> takenOnTile)
    {
        if (takenOnTile.Count == 0)
        {
            return stocks;
        }

        return
        [
            .. stocks.Select(stock =>
            {
                if (stock.Available is null)
                {
                    return stock;
                }

                var key = new FoodSourceKey(FoodSourceType.MapResource, Resource: null, stock.MapResourceInstanceId, stock.AssetId);
                ScaledAmount taken = takenOnTile.GetValueOrDefault(key);

                return stock with { Available = stock.Available.Value > taken ? stock.Available.Value - taken : ScaledAmount.Zero };
            })
        ];
    }

    /// <summary>
    /// The river edges of the tiles being fed from, keyed by coordinate.
    /// </summary>
    /// <remarks>
    /// A Game whose geometry cannot be read is a data defect, but refusing here would leave the Player unable to end their Turn. The Forces feed from the biome alone
    /// instead, which is what every tile offered before rivers enriched them.
    /// </remarks>
    private async Task<IReadOnlyDictionary<Coord, TileRivers>> ReadRiversAsync(AscendantEmpireContext db, int gameId, IReadOnlyCollection<Coord> coords)
    {
        CampaignMapGeometry? geometry = await sightDataReader.GetGeometryAsync(db, gameId);

        if (geometry is null)
        {
            logger.LogWarning("Game Id {GameId}'s map geometry was not found while feeding Forces, so no river food was counted on any tile.", gameId);

            return new Dictionary<Coord, TileRivers>();
        }

        return await campaignRiverReader.GetTileRiversAsync(db, gameId, coords, geometry);
    }

    /// <summary>
    /// One Force about to eat: where it stands, what set it eats from, and how many rations it needs this Turn.
    /// </summary>
    private sealed record FeedingForce(int UnitInstanceId, int CampaignMapTileId, int FoodSupplySetId, ScaledAmount Need);
}
