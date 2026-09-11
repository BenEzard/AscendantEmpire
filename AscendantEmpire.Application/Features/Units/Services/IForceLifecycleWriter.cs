using AscendantEmpire.Application.Common;
using AscendantEmpire.Application.Data;
using AscendantEmpire.Domain.Entities.Units;

namespace AscendantEmpire.Application.Features.Units.Services;

/// <summary>
/// The single writer for where a Force stands and whether it exists, recording each change in <see cref="MovementHistory"/> together with the change itself.
/// </summary>
/// <remarks>
/// <para>
/// Every path that creates, moves or destroys a Force goes through here, so that "a Force moved" has exactly one place it can be observed. Appending history at each call site instead would
/// eventually miss one, and a missed path fails <b>silently</b>: no error, just intelligence that never arrives, which is close to undebuggable from a bug report. See
/// <c>docs/design/intelligence-and-sightings.md</c>.
/// </para>
/// <para>
/// The rule every method keeps is <b>no record, no change</b>. If the Turn's sequence number cannot be allocated, nothing is staged or written and the caller gets a failure, so a Force is never
/// moved, created or destroyed without its row.
/// </para>
/// <para>
/// Works through the caller's context, so it joins whatever transaction they already have open. Sequence numbers come from <c>IPlayerTurnService</c> on that same context; a save that fails after
/// allocation leaves a gap in the Turn's sequence, which <c>PlayerTurn</c> documents as legal.
/// </para>
/// </remarks>
public interface IForceLifecycleWriter
{
    /// <summary>
    /// Stages a Force's move onto another tile, and the <c>Moved</c> row that records it, on the caller's context.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Does not save.</b> The caller's own <c>SaveChangesAsync</c> commits the new tile and the row in one statement batch, alongside anything else they changed about the Force - movement
    /// points, typically. On failure nothing has been staged, and the caller should refuse the move rather than save.
    /// </para>
    /// <para>
    /// The Force must be one the context is already tracking as an existing row, because the tile is set through the change tracker. A move onto the tile the Force already stands on is refused:
    /// it is not a position change, and a row whose from and to tiles matched would give the fold a step with no direction.
    /// </para>
    /// </remarks>
    /// <param name="playerTurnId">The open Turn the move happens during, which its row is stamped with.</param>
    Task<ServiceResultBase> MoveAsync(AscendantEmpireContext context, UnitInstance force, int destinationTileId, int playerTurnId, CancellationToken cancelToken = default);

    /// <summary>
    /// Adds new Forces to the context and saves them, together with the <c>Created</c> rows that record them when there is a Turn to stamp them with.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Saves, unlike the other two methods.</b> A new Force has no id until it is inserted, and its row has to carry that id - the row's <c>UnitInstanceId</c> is deliberately not a foreign key,
    /// so EF cannot fill it in by fixing up a relationship. The Forces are therefore saved first and their rows second, inside a transaction this method opens when the caller has none, so both
    /// commit or neither does. Anything else the caller has pending on the context is flushed with the first save.
    /// </para>
    /// <para>
    /// A <see langword="null"/> <paramref name="playerTurnId"/> places the Forces with no rows at all, and so needs no transaction. That is right for anything placed outside a Turn - game setup's
    /// starting armies, or a developer spawn into a Game not yet started - because nobody could have watched them appear.
    /// </para>
    /// <para>
    /// Every Force in one call shares a single sequence number: they came into existence at the same moment. On failure the context's tracked state no longer matches the database, and it should
    /// be discarded.
    /// </para>
    /// </remarks>
    /// <param name="forces">New, unsaved Forces. A Force that already has an id is refused, since it cannot be created twice.</param>
    /// <param name="playerTurnId">The open Turn to record the creation against, or <see langword="null"/> for a placement outside any Turn.</param>
    Task<ServiceResultBase> CreateAsync(AscendantEmpireContext context, IReadOnlyCollection<UnitInstance> forces, int? playerTurnId, CancellationToken cancelToken = default);

    /// <summary>
    /// Stages a Force's deletion, and the <c>Destroyed</c> row that records it, on the caller's context.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Does not save,</b> for the same reason as <see cref="MoveAsync"/>. The Force is hard-deleted; its history rows survive with their <c>UnitInstanceId</c> intact, because that column is not
    /// a foreign key, so the end-of-turn fold can still follow the path of a Force that died before it ran.
    /// </para>
    /// <para>
    /// Nothing destroys a Force yet - attrition and combat are both still to be built - so this has no callers. The caller will have to clear the Force's orders first: <c>ForceOrders</c> does hold
    /// a foreign key to the Force, and the database will refuse the delete while any remain.
    /// </para>
    /// </remarks>
    /// <param name="playerTurnId">The open Turn the destruction happens during, which its row is stamped with.</param>
    Task<ServiceResultBase> DestroyAsync(AscendantEmpireContext context, UnitInstance force, int playerTurnId, CancellationToken cancelToken = default);
}
