using AscendantEmpire.Application.Features.Game.Handlers;

using static AscendantEmpire.Application.Common.ServiceResultBase;

namespace AscendantEmpire.Application.Features.Game.Services;

public interface IGameSetupService
{
    /// <summary>
    /// Setup a Game with the provided parameters, creating the Game and its related entities in the database.
    /// </summary>
    /// <returns>The Id of the created Game</returns>
    Task<ServiceResult<int>> SetupGameAsync(SetupGameRequest request, CancellationToken cancellationToken);
}
