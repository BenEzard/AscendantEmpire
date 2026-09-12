using AscendantEmpire.Application.Common;
using AscendantEmpire.Application.Features.Intelligence.Services;

using Microsoft.Extensions.Logging;

namespace AscendantEmpire.Application.Features.Turns.Services.TurnPipeline.Campaign.EndTurn;

/// <summary>
/// Writes down what every other Player watched the acting Player do this Turn, as reports and known-Force and known-Structure memories.
/// </summary>
/// <remarks>
/// <para>
/// The orchestration only: the work is <see cref="IForceSightingFoldService"/> and then <see cref="IStructureSightingFoldService"/>. Runs last, after attrition, so a Force destroyed by hostile
/// terrain is still reported as seen. See "Where it runs" in <c>docs/design/intelligence-and-sightings.md</c>.
/// </para>
/// <para>
/// <b>A failure of either refuses the end of the Turn.</b> The pipeline stops at the first error and the Player can try again; both steps are idempotent, so the retry is safe. Ending the Turn
/// anyway would lose the sightings for good, with nothing to raise them later.
/// </para>
/// <para>
/// Forces first, then Structures. The two write different report types and different memories, so the order decides nothing about the result - only which failure is reported when both would
/// fail.
/// </para>
/// </remarks>
internal class RaiseSightingReports(
    IForceSightingFoldService forceSightingFoldService,
    IStructureSightingFoldService structureSightingFoldService,
    ILogger<RaiseSightingReports> logger) : IEndCampaignTurnProcess
{
    public string ItemName => "Raise Sighting Reports";

    public int ExecutionOrder => (int)EndCampaignTurnProcessOrder.RaiseSightingReports;

    public async Task<string> ProcessAsync(TurnPipelineContextData context)
    {
        // The end of a Turn is always Player- and Turn-scoped - EndCampaignTurnHandler refuses to run the pipeline without an open Turn. A null here is a defect in the caller, and folding with a
        // guessed Turn would raise sightings against the wrong one.
        if (context.PlayerId is not int playerId || context.PlayerTurnId is not int playerTurnId)
        {
            string error = $"{ItemName} was given an end-of-Turn context without a Player Id or Player Turn Id for Game Id {context.GameId}.";
            logger.LogError("{Error}", error);

            return error;
        }

        ServiceResultBase forceResult = await forceSightingFoldService.FoldTurnAsync(context.GameId, playerTurnId, playerId);

        if (forceResult.HasSucceeded == false)
        {
            string error = $"Force sighting reports could not be raised for Player Turn Id {playerTurnId}, so the Turn has not ended. Errors: {string.Join("; ", forceResult.Errors)}";
            logger.LogWarning("{Error}", error);

            return error;
        }

        ServiceResultBase structureResult = await structureSightingFoldService.FoldTurnAsync(context.GameId, playerTurnId, playerId);

        if (structureResult.HasSucceeded == false)
        {
            string error = $"Structure sighting reports could not be raised for Player Turn Id {playerTurnId}, so the Turn has not ended. Errors: {string.Join("; ", structureResult.Errors)}";
            logger.LogWarning("{Error}", error);

            return error;
        }

        return string.Empty;
    }
}
