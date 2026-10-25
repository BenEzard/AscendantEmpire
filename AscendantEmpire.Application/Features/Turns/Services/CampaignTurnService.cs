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
                    FactionName = p.Player.FactionName,

                    // Intentionally a correlated subquery rather than a navigation, due to volume - a lane accumulates a row per Player per Round for the life of the Game.
                    PlayerTurnId = context.PlayerTurns
                        .Where(t => t.TurnQueueId == p.TurnQueueId)
                        .Where(t => t.EndedAt == null)
                        .Select(t => (int?)t.Id)
                        .FirstOrDefault()
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
                .FirstOrDefaultAsync(cancellationToken);

            if (currentTurn is null)
            {
                logger.LogWarning("No next Campaign Turn Queue or current Player was found for Game Id: {GameId}", gameId);

                return ServiceResult<CurrentCampaignTurn>.NotFound($"No next Campaign Turn Queue or current player was found for Game Id {gameId}.");
            }

            return ServiceResult<CurrentCampaignTurn>.Succeed(currentTurn);
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
        await using AscendantEmpireContext context = await dbFactory.CreateDbContextAsync();

        return await GetFirstCampaignTurnAsync(context, gameId);
    }

    /// <inheritdoc />
    public async Task<ServiceResult<CurrentCampaignTurn>> GetFirstCampaignTurnAsync(AscendantEmpireContext context, int gameId, CancellationToken cancellationToken = default)
    {
        try
        {
            if (gameId <= 0)
            {
                logger.LogWarning("Game Id must be greater than 0. Provided Game Id: {GameId}", gameId);

                return ServiceResult<CurrentCampaignTurn>.Fail("Game Id must be greater than 0.");
            }

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
                .FirstOrDefaultAsync(cancellationToken);

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

    /// <inheritdoc />
    public async Task<int> GetCurrentRoundNumberAsync(int gameId, CancellationToken cancellationToken = default)
    {
        await using AscendantEmpireContext context = await dbFactory.CreateDbContextAsync(cancellationToken);

        // FirstOrDefaultAsync over the projected round number naturally yields 0 when no active queue matches
        // (e.g. a save made before the campaign has started) — the same sentinel used for turn-stamping
        // global/common templates, which aren't game-scoped at all. First rather than Single for the same
        // reason: this overload promises an answer, so it must not throw on a lane nobody has tidied up.
        return await CampaignTurnQueueQuery.Active(context, gameId)
            .Select(q => q.CurrentRoundNumber)
            .FirstOrDefaultAsync(cancellationToken);
    }

    /// <inheritdoc />
    public async Task<ServiceResult<int>> GetCurrentRoundNumberAsync(
        AscendantEmpireContext context,
        int gameId,
        CancellationToken cancellationToken = default)
    {
        try
        {
            // Untracked: the lane is being read for its Round, never written through this call, so a caller mid-write keeps its change tracker to itself.
            int? currentRound = await CampaignTurnQueueQuery.Active(context, gameId)
                .AsNoTracking()
                .Select(queue => (int?)queue.CurrentRoundNumber)
                .SingleOrDefaultAsync(cancellationToken);

            return currentRound is null
                ? ServiceResult<int>.NotFound($"Game {gameId} has no active campaign turn queue, so its current Round is unknown.")
                : ServiceResult<int>.Succeed(currentRound.Value);
        }
        catch (InvalidOperationException ex)
        {
            string error = $"Game {gameId} has more than one active Campaign Turn Queue, so its current Round is ambiguous.";

            logger.LogError(ex, "{Error} Game Id: {GameId}", error, gameId);

            return ServiceResult<int>.Fail(error);
        }
    }

    /// <inheritdoc />
    public async Task<ServiceResult<int>> GetObservationTurnIdAsync(AscendantEmpireContext context, int gameId, int playerId, CancellationToken cancellationToken = default)
    {
        try
        {
            // The reader's own most recent Turn in this Game's Campaign lane.
            //
            // Ordered by Id rather than StartedAt: the identity is monotonic within a lane, and two Turns can share a timestamp where two ids never can.
            //
            // Deliberately not filtered on IsCompleted - a finished lane's Turns are still that Player's history, and the stamp is about when they saw
            // something, not about whether the Game is still running.
            int? ownTurnId = await context.PlayerTurns
                .Where(t => t.TurnQueue.GameId == gameId)
                .Where(t => t.TurnQueue.QueueType == TurnQueueType.Campaign)
                .Where(t => t.TurnQueue.BattleId == null)
                .Where(t => t.PlayerId == playerId)
                .OrderByDescending(t => t.Id)
                .Select(t => (int?)t.Id)
                .FirstOrDefaultAsync(cancellationToken);

            if (ownTurnId is int ownId)
            {
                return ServiceResult<int>.Succeed(ownId);
            }

            // They have never acted. The lane's open Turn is the only honest "when" for the ground their starting Forces can already see.
            int? openTurnId = await GetOpenPlayerTurnIdAsync(context, gameId, cancellationToken);

            if (openTurnId is int openId)
            {
                return ServiceResult<int>.Succeed(openId);
            }

            // No Turns at all. The map read only runs for a Game in progress, and a Game becomes in progress in the same transaction that opens its first Turn,
            // so this is a broken invariant rather than an early read. Refuse it, rather than stamp a Turn id that means nothing.
            string error = $"Game Id {gameId} has no Campaign Player Turns to stamp Player Id {playerId}'s observations with.";
            logger.LogError("{Error}", error);

            return ServiceResult<int>.Fail(error);
        }
        catch (Exception ex)
        {
            string error = $"An error occurred while resolving the observation Turn for Game Id {gameId}, Player Id {playerId}.";
            logger.LogError(ex, "{Error}", error);

            return ServiceResult<int>.Fail(error);
        }
    }

    /// <inheritdoc />
    public async Task<int?> GetOpenPlayerTurnIdAsync(AscendantEmpireContext context, int gameId, CancellationToken cancellationToken = default)
    {
        // UX_PlayerTurn_OpenTurnPerQueue allows one open Turn per lane, so this is normally a single row. Ordered newest first anyway, so that a Game somehow holding two campaign lanes answers
        // with the most recent Turn rather than an arbitrary one.
        return await context.PlayerTurns
            .Where(t => t.TurnQueue.GameId == gameId)
            .Where(t => t.TurnQueue.QueueType == TurnQueueType.Campaign)
            .Where(t => t.TurnQueue.BattleId == null)
            .Where(t => t.EndedAt == null)
            .OrderByDescending(t => t.Id)
            .Select(t => (int?)t.Id)
            .FirstOrDefaultAsync(cancellationToken);
    }

    /// <inheritdoc />
    public async Task<int?> GetLastEndedOwnTurnIdAsync(AscendantEmpireContext context, int gameId, int playerId, CancellationToken cancellationToken = default)
    {
        // Ordered by Id rather than EndedAt, for the same reason as GetObservationTurnIdAsync: the identity is monotonic, and two Turns can share a timestamp where two ids never can.
        return await context.PlayerTurns
            .Where(t => t.TurnQueue.GameId == gameId)
            .Where(t => t.TurnQueue.QueueType == TurnQueueType.Campaign)
            .Where(t => t.TurnQueue.BattleId == null)
            .Where(t => t.PlayerId == playerId)
            .Where(t => t.EndedAt != null)
            .OrderByDescending(t => t.Id)
            .Select(t => (int?)t.Id)
            .FirstOrDefaultAsync(cancellationToken);
    }
}