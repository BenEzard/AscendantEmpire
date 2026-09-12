using AscendantEmpire.Application.Common;

namespace AscendantEmpire.Application.Features.Intelligence.Services;

/// <summary>
/// The Structure half of the sightings step: turns one Turn's structure history into reports and known-Structure memories for every Player who watched.
/// </summary>
/// <remarks>
/// <para>
/// Reads the Turn's <c>StructureHistory</c>, asks <c>IWitnessService</c> who could see each event - and, for a Structure still standing, who could still see it as the Turn ended - gathers what
/// each coalition saw into one sighting per Structure, and writes a <c>PlayerReport</c> and a <c>PlayerKnownStructure</c> per watching Player. See "What each producer emits" in
/// <c>docs/design/intelligence-and-sightings.md</c>.
/// </para>
/// <para>
/// <b>Only the acting Player is excluded from witnessing.</b> The Force step also excludes a Force's owner, because only the acting Player's Forces move during their Turn. A Structure event can
/// happen to <i>another</i> Player's Structure - the acting Player captures or razes it - and that Player's own Observers nearby watching it happen is intelligence they legitimately hold.
/// </para>
/// <para>
/// <b>Idempotent</b>, and a failure stops the Turn from ending, for the same reasons as <see cref="IForceSightingFoldService"/>. The two steps replace only their own report type, so neither re-run
/// disturbs the other's reports.
/// </para>
/// </remarks>
internal interface IStructureSightingFoldService
{
    /// <summary>
    /// Raises the structure reports and refreshes the known Structures for everything watched during one Turn, on a context of its own.
    /// </summary>
    /// <param name="playerTurnId">The Turn to fold. It should still be open: the step runs as part of ending it.</param>
    /// <param name="actingPlayerId">The Player whose Turn it is. Their coalition is never a witness to it.</param>
    Task<ServiceResultBase> FoldTurnAsync(int gameId, int playerTurnId, int actingPlayerId, CancellationToken cancelToken = default);
}
