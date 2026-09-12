using AscendantEmpire.Application.Common;
using AscendantEmpire.Application.Features.Intelligence.Services;

using Microsoft.Extensions.Logging;

namespace AscendantEmpire.Application.Features.Turns.Services.TurnPipeline.Campaign.EndTurn;

/// <summary>
/// Writes down what every other Player watched the acting Player do this Turn, as reports and known-Force and known-Structure memories.
/// </summary>
/// <remarks>
/// <para>
/// The orchestration only: the work is <see cref="IForceSightingFoldService"/>, then <see cref="IStructureSightingFoldService"/>, then <see cref="ICombatSightingFoldService"/>. Runs last, after
/// attrition, so a Force destroyed by hostile terrain is still reported as seen. See "Where it runs" in <c>docs/design/intelligence-and-sightings.md</c>.
/// </para>
/// <para>
/// <b>A failure of any of them refuses the end of the Turn.</b> The pipeline stops at the first error and the Player can try again; all three steps are idempotent, so the retry is safe. Ending
/// the Turn anyway would lose the sightings for good, with nothing to raise them later.
/// </para>
/// <para>
/// Forces, then Structures, then engagements. Each writes a different report type, and only the first two write memories, so the order decides nothing about the result - only which failure is
/// reported when more than one would fail.
/// </para>
/// <para>
/// This is the <b>campaign</b> host. The combat fold has a second one in the design, end of battle Turn, since a battle may resolve outside any campaign Turn; that host does not exist yet, which
/// is why the fold is a service rather than logic this process owns.
/// </para>
/// </remarks>
internal class RaiseSightingReports(
    IForceSightingFoldService forceSightingFoldService,
    IStructureSightingFoldService structureSightingFoldService,
    ICombatSightingFoldService combatSightingFoldService,
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

        ServiceResultBase combatResult = await combatSightingFoldService.FoldTurnAsync(context.GameId, playerTurnId, playerId);

        if (combatResult.HasSucceeded == false)
        {
            string error = $"Combat sighting reports could not be raised for Player Turn Id {playerTurnId}, so the Turn has not ended. Errors: {string.Join("; ", combatResult.Errors)}";
            logger.LogWarning("{Error}", error);

            return error;
        }

        return string.Empty;
    }
}
