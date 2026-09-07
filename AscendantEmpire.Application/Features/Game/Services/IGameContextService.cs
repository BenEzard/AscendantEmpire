using AscendantEmpire.Application.Common;
using AscendantEmpire.BackendContracts.Games.GetGameContext;

namespace AscendantEmpire.Application.Features.Game.Services;

internal interface IGameContextService
{
    /// <summary>
    /// Get the map geometry and active Players of in-progress Games, optionally narrowed to one Game and/or one Player.
    /// </summary>
    /// <remarks>
    /// <para>
    /// With both <paramref name="gameId"/> and <paramref name="playerId"/> null this returns every in-progress Game, which is only ever useful to a
    /// diagnostic. Callers normally pass at least a game id.
    /// </para>
    /// <para>
    /// An empty result is the answer to "that Game is not in progress, or that Player is not active in it", so callers use it as a membership check.
    /// </para>
    /// </remarks>
    /// <returns>A collection of Game contexts</returns>
    Task<ServiceResultBase.ServiceResult<IReadOnlyCollection<GameContext>>> GetGameContexts(int? gameId, int? playerId, CancellationToken cancelToken);
}
