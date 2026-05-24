using AscendantEmpire.Application.Data;
using AscendantEmpire.BackendContracts.Turns.GetCurrentCampaignTurn;
using AscendantEmpire.Domain.Enums.Turns;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

using static AscendantEmpire.Application.Common.ServiceResultBase;

namespace AscendantEmpire.Application.Features.Turns.Services;

public class CampaignTurnService(
    ILogger<CampaignTurnService> logger,
    IDbContextFactory<AscendantEmpireContext> dbFactory) : ICampaignTurnService
{
    /// <inheritdoc />
    public async Task<ServiceResult<CurrentCampaignTurn>> GetCurrentCampaignTurnAsync(int gameId, CancellationToken cancellationToken = default)
    {
        try
        {
            if (gameId <= 0)
            {
                logger.LogWarning("Game Id must be greater than 0. Provided Game Id: {GameId}", gameId);
                return ServiceResult<CurrentCampaignTurn>.Fail("Game Id must be greater than 0.");
            }

            await using AscendantEmpireContext context = await dbFactory.CreateDbContextAsync(cancellationToken);

            CurrentCampaignTurn? currentTurn = await context.TurnQueueParticipants
                .Where(p => p.TurnQueue.GameId == gameId)
                .Where(p => p.TurnQueue.QueueType == TurnQueueType.Campaign)
                .Where(p => p.TurnQueue.BattleId == null)
                .Where(p => p.TurnQueue.IsCompleted == false)
                .Where(p => p.IsActiveInLane)
                .Where(p => p.Status == TurnQueueParticipantStatus.TakingTurn)
                .Select(p => new CurrentCampaignTurn
                {
                    GameId = gameId,
                    TurnQueueParticipantId = p.Id,
                    TurnQueueId = p.TurnQueueId,
                    PlayerId = p.PlayerId,
                    TurnOrder = p.TurnOrder,
                    IsLastTurnInRound = p.TurnOrder == p.TurnQueue.TurnQueueParticipants
                        .Where(x => x.IsActiveInLane)
                        .Max(x => x.TurnOrder),
                    CurrentRoundNumber = p.TurnQueue.CurrentRoundNumber,
                    FactionName = p.Player.FactionName
                })
                .SingleOrDefaultAsync(cancellationToken);

            if (currentTurn is null)
            {
                logger.LogWarning("No active Campaign Turn Queue or current Player was found for Game Id: {GameId}", gameId);
                return ServiceResult<CurrentCampaignTurn>.NotFound($"No active Campaign Turn Queue or current player was found for Game Id {gameId}.");
            }

            return ServiceResult<CurrentCampaignTurn>.Succeed(currentTurn);
        }
        catch (InvalidOperationException ex)
        {
            const string error = "Multiple active Campaign Turn Queues or current players were found for the Game.";
            logger.LogError(ex, "{Error} Game Id: {GameId}", error, gameId);
            return ServiceResult<CurrentCampaignTurn>.Fail(error);
        }
        catch (Exception ex)
        {
            const string error = "An error occurred while getting current campaign turn details.";
            logger.LogError(ex, error);
            return ServiceResult<CurrentCampaignTurn>.Fail([error, ex.Message]);
        }
    }

    /// <inheritdoc />
    public async Task<ServiceResult<CurrentCampaignTurn>> GetNextCampaignTurnAsync(int gameId, int turnOrderOfLastPlayer, CancellationToken cancellationToken = default)
    {
        try
        {
            if (gameId <= 0)
            {
                logger.LogWarning("Game Id must be greater than 0. Provided Game Id: {GameId}", gameId);
                return ServiceResult<CurrentCampaignTurn>.Fail("Game Id must be greater than 0.");
            }

            await using AscendantEmpireContext context = await dbFactory.CreateDbContextAsync(cancellationToken);

            CurrentCampaignTurn? currentTurn = await context.TurnQueueParticipants
                .Where(p => p.TurnQueue.GameId == gameId)
                .Where(p => p.TurnQueue.QueueType == TurnQueueType.Campaign)
                .Where(p => p.TurnQueue.BattleId == null)
                .Where(p => p.TurnQueue.IsCompleted == false)
                .Where(p => p.IsActiveInLane)
                .Where(p => p.Status == TurnQueueParticipantStatus.Pending)
                .Where(p => p.TurnOrder > turnOrderOfLastPlayer)
                .OrderBy(p => p.TurnOrder)
                .Select(p => new CurrentCampaignTurn
                {
                    GameId = gameId,
                    TurnQueueParticipantId = p.Id,
                    TurnQueueId = p.TurnQueueId,
                    PlayerId = p.PlayerId,
                    TurnOrder = p.TurnOrder,
                    IsLastTurnInRound = p.TurnOrder == p.TurnQueue.TurnQueueParticipants
                        .Where(x => x.IsActiveInLane)
                        .Max(x => x.TurnOrder),
                    CurrentRoundNumber = p.TurnQueue.CurrentRoundNumber,
                    FactionName = p.Player.FactionName
                })
                .SingleOrDefaultAsync(cancellationToken);

            if (currentTurn is null)
            {
                logger.LogWarning("No next Campaign Turn Queue or current Player was found for Game Id: {GameId}", gameId);
                return ServiceResult<CurrentCampaignTurn>.NotFound($"No next Campaign Turn Queue or current player was found for Game Id {gameId}.");
            }

            return ServiceResult<CurrentCampaignTurn>.Succeed(currentTurn);
        }
        catch (InvalidOperationException ex)
        {
            const string error = "Multiple active Campaign Turn Queues or current players were found for the Game.";
            logger.LogError(ex, "{Error} Game Id: {GameId}", error, gameId);
            return ServiceResult<CurrentCampaignTurn>.Fail(error);
        }
        catch (Exception ex)
        {
            const string error = "An error occurred while getting next campaign turn details.";
            logger.LogError(ex, error);
            return ServiceResult<CurrentCampaignTurn>.Fail([error, ex.Message]);
        }
    }

    /// <inheritdoc />
    public async Task<ServiceResult<CurrentCampaignTurn>> GetFirstCampaignTurnAsync(int gameId)
    {
        try
        {
            if (gameId <= 0)
            {
                logger.LogWarning("Game Id must be greater than 0. Provided Game Id: {GameId}", gameId);
                return ServiceResult<CurrentCampaignTurn>.Fail("Game Id must be greater than 0.");
            }

            await using AscendantEmpireContext context = await dbFactory.CreateDbContextAsync();

            CurrentCampaignTurn? currentTurn = await context.TurnQueueParticipants
                .Where(p => p.TurnQueue.GameId == gameId)
                .Where(p => p.TurnQueue.QueueType == TurnQueueType.Campaign)
                .Where(p => p.TurnQueue.BattleId == null)
                .Where(p => p.TurnQueue.IsCompleted == false)
                .Where(p => p.IsActiveInLane)
                .Where(p => p.Status == TurnQueueParticipantStatus.Pending)
                .OrderBy(p => p.TurnOrder)
                .Select(p => new CurrentCampaignTurn
                {
                    GameId = gameId,
                    TurnQueueParticipantId = p.Id,
                    TurnQueueId = p.TurnQueueId,
                    PlayerId = p.PlayerId,
                    TurnOrder = p.TurnOrder,
                    IsLastTurnInRound = p.TurnOrder == p.TurnQueue.TurnQueueParticipants
                        .Where(x => x.IsActiveInLane)
                        .Max(x => x.TurnOrder),
                    CurrentRoundNumber = p.TurnQueue.CurrentRoundNumber,
                    FactionName = p.Player.FactionName
                })
                .FirstOrDefaultAsync();

            if (currentTurn is null)
            {
                logger.LogWarning("No active Campaign Turn Queue player was found for Game Id: {GameId}", gameId);
                return ServiceResult<CurrentCampaignTurn>.NotFound($"No active Campaign Turn Queue player was found for Game Id {gameId}.");
            }

            return ServiceResult<CurrentCampaignTurn>.Succeed(currentTurn);
        }
        catch (Exception ex)
        {
            const string error = "An error occurred while getting the first campaign turn details.";
            logger.LogError(ex, "{Error} Game Id: {GameId}", error, gameId);
            return ServiceResult<CurrentCampaignTurn>.Fail([error, ex.Message]);
        }
    }
}