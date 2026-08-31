namespace AscendantEmpire.Application.Features.ForceOrders.ContextActions;

/// <summary>
/// Loads the single <see cref="ForceActionContext"/> snapshot that every context-action strategy is judged against.
/// </summary>
/// <remarks>
/// This is the only part of the context-action pipeline that touches the database.
/// Keeping the strategies pure means new action types cost no extra queries, and the rules themselves need no database to test.
/// </remarks>
internal interface IForceActionContextBuilder
{
    /// <summary>
    /// Builds the context for a selected Force, or returns null when the Force does not exist in this game or does not belong to this player.
    /// </summary>
    Task<ForceActionContext?> BuildAsync(int gameId, int playerId, int unitInstanceId, CancellationToken cancelToken);
}
