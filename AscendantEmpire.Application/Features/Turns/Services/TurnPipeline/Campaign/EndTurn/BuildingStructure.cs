using AscendantEmpire.Application.Common;
using AscendantEmpire.Application.Data;
using AscendantEmpire.Application.Features.Structures.Services;
using AscendantEmpire.Domain.Entities.Games;

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
/// On completion the Structure's own tile becomes owned by the Player, and - where the Structure is a Settlement's - owned through that Settlement. The single tile only:
/// the territory radius of <c>docs/design/settlements.md</c> is not built.
/// </para>
/// </remarks>
internal class BuildingStructure(
    IDbContextFactory<AscendantEmpireContext> dbFactory,
    IStructureLifecycleWriter structureLifecycleWriter,
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
        int completed = 0;

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
                completed++;

                await ClaimTileForCompletedStructureAsync(db, structure);
            }
        }

        await db.SaveChangesAsync();

        logger.LogInformation("Advanced construction on {Advanced} structure(s) for Player Id {PlayerId} in Game Id {GameId}; {Completed} completed, {Failed} failed.",
            advanced, playerId, context.GameId, completed, failures.Count);

        if (failures.Count > 0)
        {
            return $"{failures.Count} structure(s) could not be advanced: {string.Join(" | ", failures)}";
        }

        // Silence is success. The pipeline reads any non-empty return as a failure and stops the Turn there, so a step that
        // reported what it did would abort every step after it - see TurnServicePipeline. What was advanced is logged above.
        return string.Empty;
    }

    /// <summary>
    /// Makes the finished Structure's own tile owned by the Player, and owned through its Settlement where it has one.
    /// </summary>
    /// <remarks>
    /// <para>
    /// A finished structure asserting ownership is the design's own rule - see "Founding a Settlement or finishing a structure" in
    /// <c>docs/design/campaign-map/tile-ownership.md</c> - and this is the first thing in the game to write any of those columns.
    /// </para>
    /// <para>
    /// <b>Its own tile, and nothing further.</b> A Settlement's territory radius, the claim extinction that ownership is supposed to bring with it, and the enclosure rules
    /// all wait on the per-round pass that tile-ownership.md describes. What is written here is the one tile the structure stands on.
    /// </para>
    /// <para>
    /// Staged on the caller's context, so it is saved with the construction that produced it.
    /// </para>
    /// </remarks>
    private async Task ClaimTileForCompletedStructureAsync(AscendantEmpireContext db, StructureInstance structure)
    {
        CampaignMap? tile = await db.CampaignMaps
            .FirstOrDefaultAsync(t => t.CampaignMapId == structure.CampaignMapTileId);

        if (tile is null)
        {
            // A Structure cannot stand on a tile that does not exist, so this is a hole in the map rather than anything the Turn did.
            logger.LogError("Structure Instance Id {StructureInstanceId} completed on Campaign Map Tile Id {TileId}, which does not exist, so no ownership was written.",
                structure.Id, structure.CampaignMapTileId);

            return;
        }

        tile.OwnedByPlayerId = structure.OwnedByPlayerId;

        // Null for a structure that is nobody's Settlement - a watch tower owns its tile but carries no Settlement, and no Settlement-scoped modifier applies to it.
        tile.OwnedBySettlementId = await db.Settlements
            .Where(settlement => settlement.StructureInstanceId == structure.Id)
            .Select(settlement => (int?)settlement.Id)
            .FirstOrDefaultAsync();
    }
}
