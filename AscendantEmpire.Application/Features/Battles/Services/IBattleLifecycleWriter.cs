using AscendantEmpire.Application.Common;
using AscendantEmpire.Application.Data;
using AscendantEmpire.Domain.Entities.Battles;
using AscendantEmpire.Domain.Enums.Battle;

namespace AscendantEmpire.Application.Features.Battles.Services;

/// <summary>
/// How one side came out of an engagement, and what it cost them.
/// </summary>
/// <param name="PlayerId">The side.</param>
/// <param name="ExitReason">Why they are out, which may not be <see cref="BattleExitReason.NotApplicable"/> - that is what a side still in the fight carries.</param>
/// <param name="TroopsLost">How many troops they lost over their whole time in the engagement.</param>
public readonly record struct BattleSideOutcome(int PlayerId, BattleExitReason ExitReason, int TroopsLost);

/// <summary>
/// Everything an engagement's ending is recorded from.
/// </summary>
/// <remarks>
/// <see cref="Outcomes"/> must cover exactly the sides still in the Battle, because concluding writes each of them out. A side that left earlier already has its own row and must not be given a
/// second one.
/// </remarks>
public sealed record BattleConclusion
{
    /// <summary>
    /// The Round the engagement ended in.
    /// </summary>
    public required int RoundNumber { get; init; }

    /// <summary>
    /// Who won it, or <see langword="null"/> when nobody did.
    /// </summary>
    public required int? VictorPlayerId { get; init; }

    /// <summary>
    /// Who was left holding the tile, or <see langword="null"/> when nobody was.
    /// </summary>
    /// <remarks>
    /// Not the same question as the victor, and recorded separately because it is the one a distant observer can answer: a victor may withdraw, and a draw leaves the ground to whoever is still
    /// standing on it.
    /// </remarks>
    public required int? GroundHeldByPlayerId { get; init; }

    /// <summary>
    /// One entry per side still in the engagement when it ended.
    /// </summary>
    public required IReadOnlyCollection<BattleSideOutcome> Outcomes { get; init; }
}

/// <summary>
/// The single writer for an engagement's life - commencing, gaining a side, losing one, and concluding - recording each change in <see cref="BattleHistory"/> together with the change itself.
/// </summary>
/// <remarks>
/// <para>
/// The Battle sibling of <c>IForceLifecycleWriter</c> and <c>IStructureLifecycleWriter</c>. Every path that changes an engagement goes through here, so that "fighting broke out at (X, Y)" has
/// exactly one place it can be observed. Appending history at each call site instead would eventually miss one, and a missed path fails <b>silently</b>: no error, just intelligence that never
/// arrives. See <c>docs/design/intelligence-and-sightings.md</c>.
/// </para>
/// <para>
/// The rule every method keeps is <b>no record, no change</b>. If the Turn's sequence numbers cannot be allocated, nothing is staged or written and the caller gets a failure.
/// </para>
/// <para>
/// Only what a <b>campaign map</b> observer could perceive is recorded. The battle map's blow-by-blow has no place in the feed, because a watcher on the next hill has no way of seeing it.
/// </para>
/// <para>
/// <b>Nothing calls this yet.</b> There is no combat subsystem: nothing in play starts a battle, reinforces one, resolves one, damages a Unit or destroys a Force. Each of these must go through it
/// when it is built:
/// <list type="bullet">
///   <item><description>starting a battle - <see cref="CommenceAsync"/>. <c>CreateBattleTurnQueueHandler</c> builds a <see cref="Battle"/> and its roster directly today; it has no production
///   caller and no route, but it is a bypass of this writer and must be routed through it;</description></item>
///   <item><description>a Force moving onto a tile with an open battle, and reinforcements arriving on a Player's campaign Turn - <see cref="JoinAsync"/>;</description></item>
///   <item><description>a retreat, a rout, and a Force destroyed in battle - <see cref="LeaveAsync"/>, paired with <c>IForceLifecycleWriter.DestroyAsync</c> so that the observer's known Force is
///   restatused rather than forgotten;</description></item>
///   <item><description>battle resolution - <see cref="ConcludeAsync"/>, which is also the only thing that will ever write <see cref="Battle.VictorId"/>.</description></item>
/// </list>
/// </para>
/// <para>
/// The rows it writes are read by <c>ICombatSightingFoldService</c>, which is keyed on a Turn rather than on a Game so that an end-of-battle-turn host can call it unchanged. Because
/// <c>PlayerTurn</c> hangs off the Turn Queue rather than the Game, and <c>IPlayerTurnService</c> is already lane-agnostic, these methods work as they stand on a battle lane's Turn once something
/// opens one.
/// </para>
/// <para>
/// Works through the caller's context, so it joins whatever transaction they already have open.
/// </para>
/// </remarks>
public interface IBattleLifecycleWriter
{
    /// <summary>
    /// Adds a new Battle and its founding roster to the context and saves them, together with the rows that record the engagement starting.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Saves, unlike the other methods,</b> for the same reason as <c>IStructureLifecycleWriter.FoundAsync</c>: the rows have to carry the new Battle's id, which it does not have until it is
    /// inserted. The Battle is saved first and its rows second, inside a transaction this method opens when the caller has none. Anything else the caller has pending is flushed with the first
    /// save, and on failure the context should be discarded.
    /// </para>
    /// <para>
    /// Writes a <c>Commenced</c> row and then one <c>ParticipantJoined</c> row per founding side, on consecutive sequences, so that a side already there when the fighting started reads the same
    /// way as one that arrived later.
    /// </para>
    /// </remarks>
    /// <param name="battle">A new, unsaved, unconcluded Battle carrying its founding <see cref="Battle.BattleParticipants"/>, which may not be empty.</param>
    /// <param name="playerTurnId">The open Turn to record the engagement starting against.</param>
    Task<ServiceResultBase> CommenceAsync(AscendantEmpireContext context, Battle battle, int playerTurnId, CancellationToken cancelToken = default);

    /// <summary>
    /// Stages a side joining an engagement already under way, and the row that records it, on the caller's context.
    /// </summary>
    /// <remarks>
    /// <b>Does not save.</b> On failure nothing has been staged, and the caller should refuse the change rather than save. A side already on the roster, and a concluded Battle, are both refused.
    /// </remarks>
    Task<ServiceResultBase> JoinAsync(AscendantEmpireContext context, Battle battle, int playerId, int roundNumber, int playerTurnId, CancellationToken cancelToken = default);

    /// <summary>
    /// Stages a side leaving an engagement still under way, and the row that records how and at what cost, on the caller's context.
    /// </summary>
    /// <remarks>
    /// <b>Does not save.</b> Use <see cref="ConcludeAsync"/> for the sides that are still in when the engagement ends - it writes them out itself, so calling this first would give them two rows.
    /// A side already out, a <see cref="BattleExitReason.NotApplicable"/> reason, negative losses and a concluded Battle are all refused.
    /// </remarks>
    /// <param name="troopsLost">That side's losses over its whole time in the engagement, which is what a watcher who saw it break is told about.</param>
    Task<ServiceResultBase> LeaveAsync(
        AscendantEmpireContext context,
        Battle battle,
        int playerId,
        BattleExitReason exitReason,
        int troopsLost,
        int roundNumber,
        int playerTurnId,
        CancellationToken cancelToken = default);

    /// <summary>
    /// Stages an engagement ending - every side still in it written out, then the conclusion itself - and the rows that record it, on the caller's context.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Does not save.</b> Writes one <c>ParticipantLeft</c> row per still-in side, in <see cref="BattleConclusion.Outcomes"/> order, and then the <c>Concluded</c> row carrying who was left
    /// holding the ground. The two are separate facts on separate sequences, so a coalition that watched one side break and then lost sight of the field learns that and no more.
    /// </para>
    /// <para>
    /// Stamps <see cref="Battle.ConcludedAtRoundNumber"/> and <see cref="Battle.VictorId"/>, and every still-in participant's exit fields. An already-concluded Battle, an off-roster victor, and an
    /// <see cref="BattleConclusion.Outcomes"/> set that does not cover exactly the still-in sides are all refused.
    /// </para>
    /// </remarks>
    Task<ServiceResultBase> ConcludeAsync(AscendantEmpireContext context, Battle battle, BattleConclusion conclusion, int playerTurnId, CancellationToken cancelToken = default);
}
