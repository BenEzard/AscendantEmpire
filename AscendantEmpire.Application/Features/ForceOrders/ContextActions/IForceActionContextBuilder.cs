namespace AscendantEmpire.Application.Features.ForceOrders.ContextActions;

/// <summary>
/// Loads the <see cref="ForceActionContext"/> snapshots that every context-action strategy is judged against — one per selected Force.
/// </summary>
/// <remarks>
/// This is the only part of the context-action pipeline that touches the database.
/// Keeping the strategies pure means new action types cost no extra queries, and the rules themselves need no database to test.
/// </remarks>
internal interface IForceActionContextBuilder
{
    /// <summary>
    /// Builds one context per selected Force, or returns null when any of them does not exist in this game or does not belong to this player.
    /// </summary>
    /// <param name="forceIds">
    /// The selected Forces. Read these as Force ids: every fielded Force is a Unit today, so they are <c>UnitInstance</c> ids, but a Command is a
    /// Force too and will arrive through this same parameter — see <c>docs/Terminology.md</c>.
    /// </param>
    /// <returns>
    /// One context per distinct id in <paramref name="forceIds"/>, ordered by Force id, or <see langword="null"/> when the selection cannot be
    /// answered for as a whole.
    /// </returns>
    /// <remarks>
    /// <para>
    /// All-or-nothing on purpose. A selection holding a Force that has been wiped out, or that belongs to someone else, is not a selection the
    /// toolbar can answer for; quietly dropping the bad id would render a toolbar that judged fewer Forces than the player has selected.
    /// </para>
    /// <para>
    /// The ordering is what keeps the disabled reasons stable: they are gathered per Force, so an unordered result would let two requests over the
    /// same selection return the same reasons in a different sequence.
    /// </para>
    /// </remarks>
    Task<IReadOnlyCollection<ForceActionContext>?> BuildAsync(
        int gameId,
        int playerId,
        IReadOnlyCollection<int> forceIds,
        CancellationToken cancelToken);
}
