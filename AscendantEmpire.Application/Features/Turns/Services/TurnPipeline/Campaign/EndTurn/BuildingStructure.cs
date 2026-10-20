using AscendantEmpire.Application.Common;
using AscendantEmpire.Application.Data;
using AscendantEmpire.Application.Features.Game.Services;
using AscendantEmpire.Application.Features.Maps.CampaignMaps.Classes;
using AscendantEmpire.Application.Features.Maps.CampaignMaps.Services;
using AscendantEmpire.Application.Features.Settlements.Classes;
using AscendantEmpire.Application.Features.Structures.Classes;
using AscendantEmpire.Application.Features.Structures.Services;
using AscendantEmpire.BackendContracts.CampaignMaps;
using AscendantEmpire.BackendContracts.Games.GetGameContext;
using AscendantEmpire.Domain.Entities.Games;
using AscendantEmpire.Domain.Enums.Settlements;
using AscendantEmpire.Domain.Enums.Structures;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace AscendantEmpire.Application.Features.Turns.Services.TurnPipeline.Campaign.EndTurn;

/// <summary>
/// Advances construction on every Structure the Player owns that is still being built, finishing the ones that reach 100%.
/// </summary>
/// <remarks>
/// <para>
/// Each Structure gains <c>BuildPointsPerTurn</c> of work each Turn until its kind's <c>BuildPointsRequired</c> is met. The catalogue says how much work a kind takes and
/// the Structure says how fast its founder works, so this process knows nothing about any particular kind - and the same kind takes longer for a smaller Force.
/// </para>
/// <para>
/// A village is 2,800 points, and 200 healthy humans on a full Turn make 1,400 of them: founded during Turn 4 it is at 50% at the end of Turn 4, reaches 100% at the end of
/// Turn 5, and is complete during Turn 6. Half that Force takes twice as long. See <c>ConstructionRateCalculator</c> for the rate.
/// </para>
/// <para>
/// <b>The owner's Turns, not Rounds.</b> This runs at the end of one Player's Turn and only touches that Player's Structures, so a Structure advances once per Turn its
/// owner takes - and a Player who has not had a Turn builds nothing.
/// </para>
/// <para>
/// Every advance goes through <see cref="IStructureLifecycleWriter.ProgressAsync"/>, which writes the <c>Progressed</c> or <c>Completed</c> row alongside the change. That
/// is what puts construction into the intelligence feed: a watching Player's known-structure marker follows the progress, and the completion is reported to them. See
/// <c>docs/design/intelligence-and-sightings.md</c>.
/// </para>
/// <para>
/// On completion the Structure asserts over the ground, as its catalogue entry says it does: a claim, or ownership. A Structure that is a Settlement's asserts over the
/// territory radius of its tier as well as its own tile, and carries that Settlement on every tile of it - see <c>SettlementTierRules</c> and "Territory" in
/// <c>docs/design/settlements.md</c>. Ownership extinguishes every rival claim on the ground it takes.
/// </para>
/// </remarks>
internal class BuildingStructure(
    IDbContextFactory<AscendantEmpireContext> dbFactory,
    IStructureLifecycleWriter structureLifecycleWriter,
    IGameContextService gameContextService,
    ICampaignTerritoryReader campaignTerritoryReader,
    ITileTerritoryService tileTerritoryService,
    ILogger<BuildingStructure> logger) : IEndCampaignTurnProcess
{
    public string ItemName => "Building Structure";

    public int ExecutionOrder => (int)EndCampaignTurnProcessOrder.BuildingStructure;

    public async Task<string> ProcessAsync(TurnPipelineContextData context)
    {
        // TODO: Construction is free. Nothing is spent to raise a Structure, so BuildPointsRequired is the only cost a building has. Materials and labour belong here once the asset
        //       bank exists to draw them from - see docs/design/settlements.md, "Asset banks". That is also why this process runs after Gathering: it is placed to be able
        //       to draw on what was just collected.
        // TODO: Rerun risk, the same one Gathering carries. The turn pipeline is not transactional, so if a later end-of-Turn process fails and the Turn is ended again,
        //       every Structure here advances a second time. Guard it (a per-Structure advanced-on-Turn stamp) once the pipeline's retry story is settled.
        // TODO: Nothing can be under construction on a battle map, because nothing builds there. The filter below says so rather than relying on it.

        ArgumentNullException.ThrowIfNull(context);

        if (context.PlayerId is null)
        {
            // The end of a Turn is always Player-scoped. A null here is a defect in the caller, and advancing every Player's construction would be worse than refusing.
            string error = $"{ItemName} was given an end-of-Turn context with no Player Id for Game Id {context.GameId}.";
            logger.LogError("{Error}", error);

            return error;
        }

        if (context.PlayerTurnId is not int playerTurnId)
        {
            // Every advance is recorded against the Turn, so without one there is nothing to stamp the history rows with and the work cannot be done honestly.
            string error = $"{ItemName} was given an end-of-Turn context with no Player Turn Id for Game Id {context.GameId}.";
            logger.LogError("{Error}", error);

            return error;
        }

        int playerId = context.PlayerId.Value;

        await using AscendantEmpireContext db = await dbFactory.CreateDbContextAsync();

        // Tracked, and with the catalogue row included: these are the entities ProgressAsync writes through, and the kind's BuildPointsRequired is half of what decides the step.
        List<StructureInstance> underConstruction = await db.StructureInstances
            .Include(structure => structure.Structure)
            .Where(structure => structure.GameId == context.GameId
                             && structure.OwnedByPlayerId == playerId
                             && structure.BattleMapTileId == null
                             && structure.RemovedOnTurnId == null
                             && structure.CompletedOnTurnId == null)
            .ToListAsync();

        if (underConstruction.Count == 0)
        {
            return string.Empty;
        }

        List<string> failures = [];
        int advanced = 0;

        // Collected rather than written one at a time. Two Settlements completing in the same Turn can have overlapping territory, and staging two claim stubs with the
        // same composite key is a change-tracking exception; one accumulated delta cannot do that, because TerritoryChanges is keyed by tile and by (tile, Player).
        List<StructureInstance> completedStructures = [];

        foreach (StructureInstance structure in underConstruction)
        {
            long buildPointsRequired = structure.Structure.BuildPointsRequired;

            // Floored for the same reason the calculator floors it: a rate of zero is a Structure that never finishes, and one placed rather than founded by anybody
            // carries no rate at all. The floor makes such a Structure absurdly slow rather than permanently unfinished.
            long pointsPerTurn = Math.Max(structure.BuildPointsPerTurn, ConstructionRateCalculator.MINIMUM_BUILD_POINTS_PER_TURN_RAW);

            // One Turn's work added to what is already done, and the writer clamps it to the total. Nothing to build means nothing to add: a marker rather than a
            // building - the totem pole is authored this way - asks for its own total of 0, which the writer reads as finished.
            long target = buildPointsRequired <= 0
                ? 0
                : structure.BuildPointsProgress + pointsPerTurn;

            // Exact, and decided the same way the writer decides it. A build now takes CalculateTurnsToComplete Turns and no fewer, where the old percentage step
            // finished a Turn early whenever the work did not divide evenly into the rate.
            bool isNowComplete = target >= buildPointsRequired;

            ServiceResultBase progressResult = await structureLifecycleWriter.ProgressAsync(db, structure, target, playerTurnId);

            if (progressResult.HasSucceeded == false)
            {
                // One Structure failing must not cost the Player the rest of their construction, nor stop the Turn ending: the pipeline reports the message and carries on.
                logger.LogWarning("Could not advance construction on Structure Instance Id {StructureInstanceId} in Game Id {GameId}: {Errors}",
                    structure.Id, context.GameId, string.Join("; ", progressResult.Errors));

                failures.Add($"Structure {structure.Id}: {string.Join("; ", progressResult.Errors)}");

                continue;
            }

            advanced++;

            if (isNowComplete)
            {
                completedStructures.Add(structure);
            }
        }

        await AssertTerritoryForCompletedStructuresAsync(db, context.GameId, completedStructures);

        await db.SaveChangesAsync();

        logger.LogInformation("Advanced construction on {Advanced} structure(s) for Player Id {PlayerId} in Game Id {GameId}; {Completed} completed, {Failed} failed.",
            advanced, playerId, context.GameId, completedStructures.Count, failures.Count);

        if (failures.Count > 0)
        {
            return $"{failures.Count} structure(s) could not be advanced: {string.Join(" | ", failures)}";
        }

        // Silence is success. The pipeline reads any non-empty return as a failure and stops the Turn there, so a step that
        // reported what it did would abort every step after it - see TurnServicePipeline. What was advanced is logged above.
        return string.Empty;
    }

    /// <summary>
    /// Makes every Structure that finished this Turn assert over its ground, as its catalogue entry says it does.
    /// </summary>
    /// <remarks>
    /// <para>
    /// A finished Structure asserting over its ground is the design's own rule - see "Structures" and "Founding a Settlement or finishing a structure" in
    /// <c>docs/design/campaign-map/tile-ownership.md</c>. What it asserts comes from <c>Structure.Asserts</c>, and how far it reaches comes from the tier of the
    /// Settlement it belongs to, so neither is decided here.
    /// </para>
    /// <para>
    /// <b>Ownership extinguishes every rival claim on the ground it takes, and does so now rather than at the end of the Round.</b> The two never coexist on a tile, and
    /// a Round of them coexisting would be visible: the map read would report the tile Owned while still naming the rival claimants, and the client would draw a rival's
    /// claim border inside somebody's territory.
    /// </para>
    /// <para>
    /// Staged on the caller's context through the same writer the per-Round pass uses, so it is saved with the construction that produced it and "ownership trumps claim"
    /// is spent in exactly one place in the codebase.
    /// </para>
    /// </remarks>
    private async Task AssertTerritoryForCompletedStructuresAsync(AscendantEmpireContext db, int gameId, IReadOnlyCollection<StructureInstance> completedStructures)
    {
        if (completedStructures.Count == 0)
        {
            return;
        }

        ServiceResultBase.ServiceResult<IReadOnlyCollection<GameContext>> gameContexts = await gameContextService.GetGameContexts(gameId, playerId: null, CancellationToken.None);
        GameContext? game = gameContexts.SuccessValue?.FirstOrDefault();

        if (game is null)
        {
            // Without the map's shape there is no way to work out a radius, and guessing one would move borders. Logged rather than thrown: the construction itself is
            // already done and correct, and a Turn must still be able to end.
            logger.LogError("Game Id {GameId} has no in-progress context, so the territory of {Count} completed Structure(s) was not asserted.",
                gameId, completedStructures.Count);

            return;
        }

        CampaignMapGeometry geometry = CampaignMapGeometry.From(game);

        // What each completed Structure asserts, and over which tiles, worked out before anything is loaded so the tiles can be read in one query.
        List<(StructureInstance Structure, int? SettlementId, IReadOnlyCollection<Coord> Tiles)> assertions = [];

        foreach (StructureInstance structure in completedStructures)
        {
            var settlement = await db.Settlements
                .Where(s => s.StructureInstanceId == structure.Id)
                .Select(s => new { s.Id, s.Tier })
                .FirstOrDefaultAsync();

            int radius = GetTerritoryRadius(structure, settlement?.Tier);

            Coord location;

            try
            {
                location = geometry.GetCoord(structure.CampaignMapTileId);
            }
            catch (ArgumentOutOfRangeException)
            {
                // A Structure standing on a tile outside this Game's block is a hole in the data rather than anything the Turn did.
                logger.LogError("Structure Instance Id {StructureInstanceId} completed on Campaign Map Tile Id {TileId}, which is not on Game Id {GameId}'s map, so no territory was asserted.",
                    structure.Id, structure.CampaignMapTileId, gameId);

                continue;
            }

            assertions.Add((structure, settlement?.Id, StructureAssertionRules.GetAssertedTiles(location, radius, geometry)));
        }

        if (assertions.Count == 0)
        {
            return;
        }

        int[] tileIds = [.. assertions
            .SelectMany(assertion => assertion.Tiles)
            .Select(geometry.GetCampaignMapId)
            .Distinct()];

        IReadOnlyCollection<TerritoryTile> tiles = await campaignTerritoryReader.GetTilesAsync(db, gameId, tileIds);

        if (tiles.Count == 0)
        {
            logger.LogError("None of the {TileCount} tile(s) asserted over by {Count} completed Structure(s) exist in Game Id {GameId}, so no territory was asserted.",
                tileIds.Length, assertions.Count, gameId);

            return;
        }

        TerritoryChanges changes = BuildAssertionChanges(geometry, tiles, assertions);

        if (changes.IsEmpty)
        {
            return;
        }

        ServiceResultBase applied = await tileTerritoryService.ApplyAsync(db, gameId, changes);

        if (applied.HasSucceeded == false)
        {
            logger.LogError("Could not write the territory of {Count} completed Structure(s) in Game Id {GameId}: {Errors}",
                assertions.Count, gameId, string.Join("; ", applied.Errors));
        }
    }

    /// <summary>
    /// Turns every completed Structure's assertion into one accumulated diff.
    /// </summary>
    /// <remarks>
    /// Run through a <see cref="TerritoryWorkingSet"/> rather than assembled by hand, so the assertions see each other: where two Settlements' territory overlaps, the
    /// second reads what the first already took and passes over it, and "ownership trumps claim" happens in the same place it happens for the per-Round pass.
    /// </remarks>
    private static TerritoryChanges BuildAssertionChanges(
        CampaignMapGeometry geometry,
        IReadOnlyCollection<TerritoryTile> tiles,
        IReadOnlyCollection<(StructureInstance Structure, int? SettlementId, IReadOnlyCollection<Coord> Tiles)> assertions)
    {
        // A partial map: only the tiles being asserted over. That is all these writes touch, and the working set never walks off them.
        TerritoryMap map = TerritoryMap.Create(geometry, tiles);

        int[] playerIds = [.. assertions.Select(assertion => assertion.Structure.OwnedByPlayerId).Distinct()];
        TerritoryWorkingSet working = TerritoryWorkingSet.From(map, playerIds);

        foreach ((StructureInstance structure, int? settlementId, IReadOnlyCollection<Coord> assertedTiles) in assertions)
        {
            int playerId = structure.OwnedByPlayerId;

            foreach (Coord coord in assertedTiles)
            {
                (int X, int Y) key = (coord.X, coord.Y);
                TerritoryTile? tile = map.Get(key);

                if (tile is null)
                {
                    continue;
                }

                if (structure.Structure.Asserts == StructureAssertion.Owned)
                {
                    if (TerritoryClaimability.CanOwn(tile, working.GetOwner(key), playerId))
                    {
                        // The Settlement rides on every tile of its territory, not just the one the Structure stands on, which is what lets a Settlement-scoped modifier reach them.
                        working.AssertOwnership(key, playerId, settlementId);
                    }

                    continue;
                }

                if (TerritoryClaimability.CanClaim(tile, working.GetOwner(key), playerId))
                {
                    working.AssertClaim(key, playerId);
                }
            }
        }

        return working.ToChanges();
    }

    /// <summary>
    /// How far out a completed Structure asserts: 0 for one that is nobody's Settlement, otherwise its tier's radius.
    /// </summary>
    /// <remarks>
    /// A watch tower asserts over the ground it stands on and carries no Settlement, so no Settlement-scoped modifier applies to it. A tier with no authored radius is
    /// logged and treated as the single tile, because growth is not built and a loud log beats a silent guess at how much ground a grown city should take.
    /// </remarks>
    private int GetTerritoryRadius(StructureInstance structure, SettlementTier? tier)
    {
        if (tier is not SettlementTier settlementTier)
        {
            return 0;
        }

        if (SettlementTierRules.TryGetTerritoryRadius(settlementTier, out int radius))
        {
            return radius;
        }

        logger.LogWarning("Settlement tier {Tier} has no authored territory radius, so Structure Instance Id {StructureInstanceId} asserted over its own tile only.",
            settlementTier, structure.Id);

        return 0;
    }
}
