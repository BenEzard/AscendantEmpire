using AscendantEmpire.Application.Common;
using AscendantEmpire.Application.Features.Game.Services;
using AscendantEmpire.BackendContracts.Games.GetGameContext;
using AscendantEmpire.BackendContracts.Turns.GetCurrentCampaignTurn;

using Microsoft.Extensions.Logging;

using static AscendantEmpire.Application.Common.ServiceResultBase;

namespace AscendantEmpire.Application.Features.Turns.Services;

/// <inheritdoc cref="ICampaignTurnAccessService"/>
internal sealed class CampaignTurnAccessService(
    ILogger<CampaignTurnAccessService> logger,
    IGameContextService gameContextService,
    ICampaignTurnService campaignTurnService) : ICampaignTurnAccessService
{
    /// <inheritdoc />
    public async Task<CampaignTurnAccess> GetCampaignTurnAccessAsync(int gameId, int playerId, CancellationToken cancelToken)
    {
        // Membership first. An empty context collection is the game context service's way of saying "that Game is not in progress, or that Player is not
        // active in it", and neither is worth a Turn lookup.
        ServiceResult<IReadOnlyCollection<GameContext>> gameContextResult = await gameContextService.GetGameContexts(gameId, playerId, cancelToken);

        if (gameContextResult.HasSucceeded == false || gameContextResult.SuccessValue is null || gameContextResult.SuccessValue.Count == 0)
        {
            string error = $"Game Id {gameId} was not found or Player Id {playerId} is not part of the game.";
            logger.LogWarning("{Error}", error);

            return new CampaignTurnAccess
            {
                Status = CampaignTurnAccessStatus.PlayerNotInGame,
                Errors = [error]
            };
        }

        ServiceResult<CurrentCampaignTurn> turnResult = await campaignTurnService.GetCurrentCampaignTurnAsync(gameId, cancelToken);

        if (turnResult.HasSucceeded == false || turnResult.SuccessValue is null)
        {
            logger.LogWarning("Failed to retrieve current campaign turn for Game Id {GameId}. Errors: {Errors}", gameId, string.Join("; ", turnResult.Errors));

            // A Game with nobody taking a Turn answers NotFound, which is an ordinary state — a Game set up but not yet started, or one between Rounds —
            // rather than a fault. Anything else genuinely went wrong.
            CampaignTurnAccessStatus status = turnResult.StatusCode == ServiceResultStatus.NotFound
                ? CampaignTurnAccessStatus.NoTurnInProgress
                : CampaignTurnAccessStatus.Failed;

            return new CampaignTurnAccess
            {
                Status = status,
                Errors = turnResult.Errors
            };
        }

        return new CampaignTurnAccess
        {
            Status = CampaignTurnAccessStatus.Granted,
            CurrentTurn = turnResult.SuccessValue
        };
    }
}
