namespace AscendantEmpire.Application.Features.TroopOrders.ContextActions;

/// <summary>
/// Loads the single <see cref="TroopActionContext"/> snapshot that every context-action strategy is judged against.
/// </summary>
/// <remarks>
/// This is the only part of the context-action pipeline that touches the database.
/// Keeping the strategies pure means new action types cost no extra queries, and the rules themselves need no database to test.
/// </remarks>
internal interface ITroopActionContextBuilder
{
    /// <summary>
    /// Builds the context for a selected troop, or returns null when the troop does not exist in this game or does not belong to this player.
    /// </summary>
    Task<TroopActionContext?> BuildAsync(int gameId, int playerId, int unitInstanceId, CancellationToken cancelToken);
}
