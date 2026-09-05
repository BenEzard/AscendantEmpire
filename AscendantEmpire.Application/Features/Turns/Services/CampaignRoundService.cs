using AscendantEmpire.Application.Common;
using AscendantEmpire.Application.Data;
using AscendantEmpire.Application.Features.Turns.Services.TurnPipeline;
using AscendantEmpire.Application.Features.Turns.Services.TurnPipeline.Campaign.StartRound;
using AscendantEmpire.Application.Features.Turns.Services.TurnPipeline.Campaign.StartTurn;
using AscendantEmpire.BackendContracts.Turns.GetCurrentCampaignTurn;
using AscendantEmpire.Domain.Enums.Games;
using AscendantEmpire.Domain.Enums.Turns;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

using static AscendantEmpire.Application.Common.ServiceResultBase;

namespace AscendantEmpire.Application.Features.Turns.Services;

/// <inheritdoc cref="ICampaignRoundService" />
internal class CampaignRoundService(
    ILogger<CampaignRoundService> logger,
    ICampaignTurnService campaignTurnService,
    ITurnServicePipeline<IStartCampaignRoundProcess> startCampaignRoundPipeline,
    ITurnServicePipeline<IStartCampaignTurnProcess> startCampaignTurnPipeline) : ICampaignRoundService
{
    /// <summary>
    /// How long a Player has to take their Turn before it is scheduled to end.
    /// </summary>
    /// <remarks>
    /// The single source of truth for the Turn clock. Nothing enforces it yet - no code writes
    /// <see cref="TurnQueueParticipantStatus.TurnCompleted_Timeout"/> - so today it only drives what the client displays.
    /// </remarks>
    private const int _TURN_MAX_DURATION_HOURS = 24; // TODO: Make the turn time configurable as a property of the Game.

    /// <inheritdoc />
    public async Task<ServiceResultBase> StartRoundAsync(AscendantEmpireContext context, int turnQueueId, int roundNumber, int gameId)
    {
        try
        {
            // Update the Turn Queue Participant records to start the Round - all active participants to Pending status.
            //
            // Resetting *all* the Participants is what makes the Round playable at all. Both GetFirstCampaignTurnAsync and
            // GetNextCampaignTurnAsync only ever look at Pending Participants, and by the time a Round closes every one of them has been
            // marked completed, so leaving any of them completed would drop that Player out of the Game for good.
            int updated = await context.TurnQueueParticipants
                .Where(x => x.TurnQueueId == turnQueueId)
                .Where(x => x.IsActiveInLane)
                .ExecuteUpdateAsync(setters => setters
                    .SetProperty(x => x.Status, TurnQueueParticipantStatus.Pending),
                    CancellationToken.None);

            if (updated == 0)
            {
                logger.LogWarning("No Turn Queue Participant records were updated for Start of Round, Turn Queue Id {TurnQueueId}.", turnQueueId);
                return ServiceResult.Fail("Failed to update Turn Queue Participant records to start the Round.");
            }

            // Update the Turn Queue record to start the Round - move it on to the new Round number
            DateTimeOffset now = DateTimeOffset.UtcNow;
            updated = await context.TurnQueues
                .Where(q => q.Id == turnQueueId)
                .ExecuteUpdateAsync(setters => setters
                    .SetProperty(q => q.CurrentRoundNumber, roundNumber)
                    .SetProperty(q => q.UpdatedAt, now),
                    CancellationToken.None);

            if (updated == 0)
            {
                logger.LogWarning("No Turn Queue record was updated for Start of Round, Turn Queue Id {TurnQueueId}.", turnQueueId);
                return ServiceResult.Fail("Failed to update Turn Queue record to start the Round.");
            }

            // Then process the Start Round pipeline
            var startRoundContext = new TurnPipelineContextData(TurnPipelineType.StartCampaignRound, gameId, roundNumber);
            ServiceResultBase startRoundResult = await startCampaignRoundPipeline.ProcessPipelineAsync(startRoundContext);

            if (startRoundResult.HasSucceeded == false)
            {
                string error = $"An error occurred during start round pipeline processing. Errors: {string.Join("; ", startRoundResult.Errors)}";
                logger.LogError("{Error}", error);
                return ServiceResultBase.Fail(error);
            }

            // With everybody Pending again, the lowest turn order leads the Round.
            // This has to come *after* the reset above: the query only considers Pending Participants, so asking any earlier would find nobody and leave the Round with no Player in it.
            //
            // Read through the caller's context, not a fresh one.
            // When a slot claim starts a Game the resets above are still uncommitted, and a second connection would either miss them or block on the rows they hold.
            ServiceResult<CurrentCampaignTurn> firstTurnResult = await campaignTurnService.GetFirstCampaignTurnAsync(context, gameId);

            if (firstTurnResult.HasSucceeded == false || firstTurnResult.SuccessValue is null)
            {
                string error = $"An error occurred while retrieving the first Campaign turn of Round {roundNumber}.";
                logger.LogError("{Error} Errors: {Errors}", error, string.Join(Environment.NewLine, firstTurnResult.Errors));

                return ServiceResultBase.Fail(error);
            }

            // Hand the Turn to them.
            ServiceResultBase startFirstTurnResult = await StartNextTurnAsync(context, firstTurnResult.SuccessValue);

            if (startFirstTurnResult.HasSucceeded == false)
            {
                string error = $"An error occurred while starting the first Campaign turn of Round {roundNumber}.";
                logger.LogError("{Error} Errors: {Errors}", error, string.Join(Environment.NewLine, startFirstTurnResult.Errors));

                return ServiceResultBase.Fail(startFirstTurnResult.Errors);
            }
        }
        catch (Exception e)
        {
            string error = $"An exception occurred during start round processing for Game Id {gameId}, Round Number {roundNumber}.";
            logger.LogError(e, "{Error}", error);
            return ServiceResultBase.Fail(error);
        }

        return ServiceResultBase.Succeed();
    }

    /// <inheritdoc />
    public async Task<ServiceResultBase> StartNextTurnAsync(AscendantEmpireContext context, CurrentCampaignTurn newTurn)
    {
        try
        {
            var startTurnContext = new TurnPipelineContextData(TurnPipelineType.StartCampaignTurn, newTurn.GameId, newTurn.PlayerId, newTurn.CurrentRoundNumber);
            ServiceResultBase startTurnResult = await startCampaignTurnPipeline.ProcessPipelineAsync(startTurnContext);

            if (startTurnResult.HasSucceeded == false)
            {
                string error = $"An error occurred during start turn pipeline processing. Errors: {string.Join("; ", startTurnResult.Errors)}";
                logger.LogError("{Error}", error);
                return ServiceResultBase.Fail(error);
            }

            // Update the Turn Queue Participant record to start the turn
            DateTimeOffset now = DateTimeOffset.UtcNow;
            int updated = await context.TurnQueueParticipants
                .Where(x => x.Id == newTurn.TurnQueueParticipantId)
                .ExecuteUpdateAsync(setters => setters
                    .SetProperty(x => x.Status, TurnQueueParticipantStatus.TakingTurn)
                    .SetProperty(x => x.LastTurnStartedAt, now),
                    CancellationToken.None);

            if (updated == 0)
            {
                logger.LogWarning("No Turn Queue Participant record was updated for Participant Id {ParticipantId}.", newTurn.TurnQueueParticipantId);
                return ServiceResult.Fail("Failed to update Turn Queue Participant record to start the turn.");
            }

            // Update the Turn Queue record to start the turn
            updated = await context.TurnQueues
                .Where(q => q.Id == newTurn.TurnQueueId)
                .ExecuteUpdateAsync(setters => setters
                    .SetProperty(q => q.CurrentTurnStartAt, now)
                    .SetProperty(q => q.CurrentScheduledTurnEndAt, now.AddHours(_TURN_MAX_DURATION_HOURS))
                    .SetProperty(q => q.CurrentActualTurnEndAt, (DateTimeOffset?)null)
                    .SetProperty(q => q.UpdatedAt, now),
                    CancellationToken.None);

            if (updated == 0)
            {
                logger.LogWarning("No Turn Queue record was updated for Turn Queue Id {TurnQueueId}.", newTurn.TurnQueueId);
                return ServiceResult.Fail("Failed to update Turn Queue record to start the turn.");
            }

            // TODO: Notify the Player that their Turn has started. 
            //       The established "something changed" pattern is enqueueing on IBackgroundProcessingChannel and letting the client poll, as useProcessStatus does.
        }
        catch (Exception ex)
        {
            string error = $"An error occurred while starting the next turn for Game Id {newTurn.GameId}, Turn Queue Id {newTurn.TurnQueueId}, Participant Id {newTurn.TurnQueueParticipantId}.";
            logger.LogError(ex, "{Error}", error);
            return ServiceResultBase.Fail(error);
        }

        return ServiceResultBase.Succeed();
    }

    /// <inheritdoc />
    public async Task<ServiceResultBase> StartGameAsync(AscendantEmpireContext context, int gameId, CancellationToken cancelToken = default)
    {
        try
        {
            // Move the Game on only if it is still waiting for Players. Making the status change the thing that decides whether we go on is what keeps this safe:
            // if two final slot claims interleave, the database lets exactly one of them past this update, and the loser leaves the Game alone rather than opening Round 1 a second time.
            int gamesStarted = await context.GameIndex
                .Where(g => g.Id == gameId)
                .Where(g => g.PlayStatus == GamePlayStatus.AwaitingPlayers)
                .ExecuteUpdateAsync(setters => setters
                    .SetProperty(g => g.PlayStatus, GamePlayStatus.InProgress),
                    cancelToken);

            if (gamesStarted == 0)
            {
                logger.LogInformation("Game Id {GameId} was asked to start but is not awaiting players, so it has been left alone.", gameId);
                return ServiceResultBase.Succeed();
            }

            // The Campaign Turn Queue and its Participants were built during setup, so Round 1 already exists and only needs opening.
            var queue = await context.TurnQueues
                .Where(q => q.GameId == gameId)
                .Where(q => q.QueueType == TurnQueueType.Campaign)
                .Where(q => q.BattleId == null)
                .Where(q => q.IsCompleted == false)
                .Select(q => new { q.Id, q.CurrentRoundNumber })
                .SingleOrDefaultAsync(cancelToken);

            if (queue is null)
            {
                string error = $"No active Campaign Turn Queue was found for Game Id {gameId}, so the Game cannot be started.";
                logger.LogError("{Error}", error);
                return ServiceResultBase.Fail(error);
            }

            // Opened at its existing Round number rather than the next one: setup created the Queue on Round 1 and nobody has played it.
            ServiceResultBase startRoundResult = await StartRoundAsync(context, queue.Id, queue.CurrentRoundNumber, gameId);

            if (startRoundResult.HasSucceeded == false)
            {
                string error = $"An error occurred while opening the first Round of Game Id {gameId}.";
                logger.LogError("{Error} Errors: {Errors}", error, string.Join(Environment.NewLine, startRoundResult.Errors));

                return ServiceResultBase.Fail(startRoundResult.Errors);
            }

            logger.LogInformation("Game Id {GameId} started on Round {RoundNumber}.", gameId, queue.CurrentRoundNumber);
        }
        catch (InvalidOperationException ex)
        {
            string error = $"Multiple active Campaign Turn Queues were found for Game Id {gameId}, so the Game cannot be started.";
            logger.LogError(ex, "{Error}", error);
            return ServiceResultBase.Fail(error);
        }
        catch (Exception ex)
        {
            string error = $"An exception occurred while starting Game Id {gameId}.";
            logger.LogError(ex, "{Error}", error);
            return ServiceResultBase.Fail(error);
        }

        return ServiceResultBase.Succeed();
    }
}
