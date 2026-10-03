namespace AscendantEmpire.Application.Features.Technologies.Services;

/// <summary>
/// The single owner of whether a Player's technology is Locked or Available.
/// </summary>
/// <remarks>
/// <para>
/// <c>PlayerTechnology.Status</c> is a materialised cache of a pure function: a technology is available once all of its mandatory prerequisites are discovered
/// and, where it has optional ones, at least one of those is too. Storing it keeps the hot end-of-turn query to a single indexed read, at the cost of needing
/// one place that puts it right.
/// </para>
/// <para>
/// Call it after any discovery, at the start of a Turn, and after any seed that touched the technology catalogue. <b>That last one is the reason this exists
/// as a service rather than a step inside the discovery handler.</b> Seeding a new technology, or a new prerequisite edge, invalidates the stored statuses of
/// every Player in every existing game - and leaves them with no row at all for a technology that did not exist when their game was set up. There is no path
/// back from that except a reconcile that backfills and recomputes.
/// </para>
/// <para>
/// Being idempotent is what makes it safe to call freely and makes every race on it benign. Do not optimise it away into the callers.
/// </para>
/// </remarks>
internal interface IPlayerTechnologyAvailabilityService
{
    /// <summary>
    /// Backfills any missing rows and recomputes every Locked/Available status for one Player.
    /// </summary>
    /// <remarks>
    /// Unlocked and Discovered rows are left alone: both are facts about what the Player did, not derivations, and a technology does not become unreachable
    /// again because its prerequisites were re-authored.
    /// </remarks>
    /// <param name="context">The context to work in, so this can join a caller's unit of work. The caller saves.</param>
    /// <param name="gameId">The Game the Player belongs to.</param>
    /// <param name="playerId">The Player to reconcile.</param>
    /// <param name="cancellationToken">Cancels the work in flight.</param>
    /// <returns>How many rows had their status changed or were added.</returns>
    Task<int> ReconcileAsync(
        Data.AscendantEmpireContext context,
        int gameId,
        int playerId,
        CancellationToken cancellationToken);

    /// <summary>
    /// Reconciles every Player in one Game, reading the catalogue once rather than once per Player.
    /// </summary>
    /// <param name="context">The context to work in. The caller saves.</param>
    /// <param name="gameId">The Game whose Players to reconcile.</param>
    /// <param name="cancellationToken">Cancels the work in flight.</param>
    /// <returns>How many rows had their status changed or were added.</returns>
    Task<int> ReconcileGameAsync(
        Data.AscendantEmpireContext context,
        int gameId,
        CancellationToken cancellationToken);
}
