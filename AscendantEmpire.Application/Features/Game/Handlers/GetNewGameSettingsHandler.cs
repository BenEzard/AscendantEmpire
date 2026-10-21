using AscendantEmpire.Application.Common.Messaging;
using AscendantEmpire.Application.Options;
using AscendantEmpire.BackendContracts.Games.GetNewGameSettings;

using Microsoft.Extensions.Options;

using static AscendantEmpire.Application.Common.ServiceResultBase;

namespace AscendantEmpire.Application.Features.Game.Handlers;

public record GetNewGameSettingsRequest() : IRequest<ServiceResult<NewGameSettings>>;

internal sealed class GetNewGameSettingsHandler(IOptions<GameSettings> gameSettings) 
    : IRequestHandler<GetNewGameSettingsRequest, ServiceResult<NewGameSettings>>      
{
    public Task<ServiceResult<NewGameSettings>> Handle(GetNewGameSettingsRequest request, CancellationToken cancellationToken)
    {
        var settings = new NewGameSettings
        {
            MinimumPlayersPerGame = gameSettings.Value.MinimumPlayersPerGame,
            MaximumPlayersPerGame = gameSettings.Value.MaximumPlayersPerGame,
            MinimumBattleMapSize = gameSettings.Value.BattleMapSettings.MinimumBattleMapSize,
            MaximumBattleMapSize = gameSettings.Value.BattleMapSettings.MaximumBattleMapSize,
            DefaultBattleMapSize = gameSettings.Value.BattleMapSettings.DefaultBattleMapSize,
            BattleMapSizeIncrement = gameSettings.Value.BattleMapSettings.BattleMapSizeIncrement,
            BaseResearchPoints = gameSettings.Value.TechnologySettings.BaseResearchPoints,
            TechnologySpeedMultiplierPercent = gameSettings.Value.TechnologySettings.TechnologySpeedMultiplierPercent,
            TechnologyFuzzinessPercent = gameSettings.Value.TechnologySettings.TechnologyFuzzinessPercent
        };
        return Task.FromResult(ServiceResult<NewGameSettings>.Succeed(settings));
    }
}
