using AscendantEmpire.Application.Common;

namespace AscendantEmpire.Application.Features.Intelligence.Services;

/// <summary>
/// The combat half of the sightings step: turns one Turn's battle history into reports for every Player who watched the fighting from the campaign map.
/// </summary>
/// <remarks>
/// <para>
/// Reads the Turn's <c>BattleHistory</c>, asks <c>IWitnessService</c> who could see each event - and, for an engagement still being fought, who could still see it as the Turn ended - gathers what
/// each coalition saw into one sighting per engagement, and writes a <c>PlayerReport</c> per watching Player. See "What each producer emits" in
/// <c>docs/design/intelligence-and-sightings.md</c>.
/// </para>
/// <para>
/// <b>Reports only.</b> Unlike the other two halves there is no known-object memory to refresh: an engagement is a point-in-time event, not a thing standing somewhere to be remembered the
/// position of. A Force watched dying in the fighting is restatused by the <see cref="IForceSightingFoldService"/> half, from the <c>Destroyed</c> row the Force writer leaves.
/// </para>
/// <para>
/// <b>The acting Player and every side in the engagement are excluded from witnessing.</b> The Structure half excludes only the acting Player, because a Structure is a thing being acted upon and
/// its owner watching it taken is real intelligence. A combatant is an <i>actor</i>: they are in the battle, with a roster place and a battle lane of their own, so an outsider's report of it
/// would be nonsense. Crucially a combatant need not be the acting Player - a battle between A and C during A's Turn leaves C non-acting - so this is a rule the acting-Player exclusion does not
/// already cover. Because the excluded set differs per engagement, this half issues one witness query per Battle rather than one for the Turn.
/// </para>
/// <para>
/// <b>Idempotent</b>, and a failure stops the Turn from ending, for the same reasons as <see cref="IForceSightingFoldService"/>. Each half replaces only its own report type, so no re-run disturbs
/// another's reports.
/// </para>
/// <para>
/// Keyed on a Turn rather than on a Game deliberately: <c>docs/design/intelligence-and-sightings.md</c> gives the combat fold <b>two</b> hosts, end of campaign Turn and end of battle Turn, since
/// a battle may resolve outside any campaign Turn. That is why it is a service the pipeline calls rather than pipeline logic of its own. <b>Only the campaign host exists today</b> - there is no
/// battle turn pipeline and battle lanes open no Turns - but nothing here needs to change when one is built.
/// </para>
/// </remarks>
internal interface ICombatSightingFoldService
{
    /// <summary>
    /// Raises the combat reports for every engagement watched during one Turn, on a context of its own.
    /// </summary>
    /// <param name="playerTurnId">The Turn to fold. It should still be open: the step runs as part of ending it.</param>
    /// <param name="actingPlayerId">The Player whose Turn it is. Their coalition is never a witness to it, and nor is any side in the engagement.</param>
    Task<ServiceResultBase> FoldTurnAsync(int gameId, int playerTurnId, int actingPlayerId, CancellationToken cancelToken = default);
}
