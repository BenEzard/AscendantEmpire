using AscendantEmpire.Application.Common;
using AscendantEmpire.BackendContracts.Games.GetOpenGames;

namespace AscendantEmpire.Application.Features.Game.Services;

internal interface IOpenGameService
{
    /// <summary>
    /// Get Open Games for the specified game and/or player. If both gameId and playerId are null, returns all open games.
    /// </summary>
    /// <returns>A collection of open Games</returns>
    Task<ServiceResultBase.ServiceResult<IReadOnlyCollection<OpenGame>>> GetOpenGames(int? gameId, int? playerId, CancellationToken cancelToken);
}