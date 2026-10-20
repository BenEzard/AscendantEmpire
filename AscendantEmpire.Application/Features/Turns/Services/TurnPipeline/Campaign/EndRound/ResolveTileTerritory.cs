using AscendantEmpire.Application.Common;
using AscendantEmpire.Application.Features.Maps.CampaignMaps.Classes;
using AscendantEmpire.Application.Features.Maps.CampaignMaps.Services;

using Microsoft.Extensions.Logging;

namespace AscendantEmpire.Application.Features.Turns.Services.TurnPipeline.Campaign.EndRound;

/// <summary>
/// Settles the Game's borders at the end of the Round: absorbs wilderness a Player has ringed, and lapses the claims nobody can supply.
/// </summary>
/// <remarks>
/// <para>
/// The per-Round pass of <c>docs/design/campaign-map/tile-ownership.md</c>. The rules themselves are pure and live in <see cref="TileTerritory"/>; this process is only
/// the pipeline's door into them, in the same way <c>RegenerateTileSustenance</c> is the door into the regrowth calculator.
/// </para>
/// <para>
/// <b>No rerun guard, and it needs none.</b> The processes beside it accumulate - construction advances, tiles regrow - so running a Round twice does their work twice,
/// and both carry a TODO about it. This one recomputes the whole territory from the tile columns and the claim rows and writes a diff, so a second run on the same state
/// decides exactly the same thing and writes nothing.
/// </para>
/// <para>
/// Player-neutral: borders are a fact about the Game rather than about whose Turn it was, so this runs once for the Round rather than once per Player.
/// </para>
/// </remarks>
internal class ResolveTileTerritory(
    ITileTerritoryService tileTerritoryService,
    ILogger<ResolveTileTerritory> logger) : IEndCampaignRoundProcess
{
    public string ItemName => "Resolve Tile Territory";

    public int ExecutionOrder => (int)EndCampaignRoundProcessOrder.ResolveTileTerritory;

    public async Task<string> ProcessAsync(TurnPipelineContextData context)
    {
        // TODO: The rest of the per-Round pass is still to come, and none of it is idempotent the way this is: the rebellion countdown stepping down, the Abandoned
        //       five-Turn revert and the attrition timers all accumulate. The first of them to join this process needs the per-Round stamp RegenerateTileSustenance and
        //       BuildingStructure are both waiting on - the freedom from one here is earned by writing a diff, and must not be inherited by accident.
        // TODO: An ally's claimed and owned tiles should count towards the claim supply range and should seal an enclosure ring. There is no alliance model to read, so
        //       ClaimSupplyRange takes a friendly-Player set that currently holds one id - see docs/design/campaign-map/tile-ownership.md, "Allies".

        ArgumentNullException.ThrowIfNull(context);

        ServiceResultBase.ServiceResult<TerritoryChanges> result = await tileTerritoryService.ResolveGameTerritoryAsync(context.GameId);

        if (result.HasSucceeded == false)
        {
            // Returning the failure stops the Round ending, which is right: a half-resolved border is worse than a Round that refuses to close.
            string error = $"{ItemName} could not resolve the territory of Game Id {context.GameId}: {string.Join("; ", result.Errors)}";
            logger.LogError("{Error}", error);

            return error;
        }

        TerritoryChanges changes = result.SuccessValue ?? TerritoryChanges.Empty;

        if (changes.IsEmpty)
        {
            // Much the commonest outcome: no border moved this Round.
            return string.Empty;
        }

        logger.LogInformation(
            "Resolved territory in Game Id {GameId} at the end of Round {RoundNumber}: {TileCount} tile(s) changed, {ClaimsAdded} claim(s) added, {ClaimsRemoved} removed.",
            context.GameId,
            context.CurrentRoundNumber,
            changes.AffectedTileCount,
            changes.ClaimsToAdd.Count,
            changes.ClaimsToRemove.Count);

        // Silence is success. The pipeline reads any non-empty return as a failure and stops the Round there, so a step that reported what it did would abort every step
        // after it - see TurnServicePipeline. What changed is logged above.
        return string.Empty;
    }
}
