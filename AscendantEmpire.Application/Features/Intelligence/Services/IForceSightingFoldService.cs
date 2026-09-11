using AscendantEmpire.Application.Common;

namespace AscendantEmpire.Application.Features.Intelligence.Services;

/// <summary>
/// The sightings step: turns one Turn's movement history into reports and known-Force memories for every Player who watched.
/// </summary>
/// <remarks>
/// <para>
/// Reads the Turn's <c>MovementHistory</c>, asks <c>IWitnessService</c> who could see each step, folds the visible steps into tracks per coalition, and writes a <c>PlayerReport</c> per track per
/// watching Player and a <c>PlayerKnownForce</c> per Force per watching Player. See <c>docs/design/intelligence-and-sightings.md</c>.
/// </para>
/// <para>
/// <b>Idempotent.</b> Running a Turn again replaces its reports rather than adding to them, and never lets an older sighting overwrite a newer memory. The end of a Turn is not one transaction, so a
/// retried end of Turn can run this twice.
/// </para>
/// <para>
/// <b>Failure stops the Turn from ending.</b> The caller, <c>RaiseSightingReports</c>, turns a failure here into a pipeline error. Letting the Turn end anyway would lose the sightings for good,
/// silently, since nothing would ever raise them later.
/// </para>
/// </remarks>
internal interface IForceSightingFoldService
{
    /// <summary>
    /// Raises the reports and refreshes the known Forces for everything watched during one Turn, on a context of its own.
    /// </summary>
    /// <param name="playerTurnId">The Turn to fold. It should still be open: the step runs as part of ending it.</param>
    /// <param name="actingPlayerId">The Player whose Turn it is. Their coalition is never a witness to it.</param>
    Task<ServiceResultBase> FoldTurnAsync(int gameId, int playerTurnId, int actingPlayerId, CancellationToken cancelToken = default);
}
