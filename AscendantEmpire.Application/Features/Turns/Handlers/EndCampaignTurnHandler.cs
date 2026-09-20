using AscendantEmpire.Application.Common;
using AscendantEmpire.Application.Common.Messaging;
using AscendantEmpire.Application.Common.Validation;
using AscendantEmpire.Application.Data;
using AscendantEmpire.Application.Features.Turns.Services;
using AscendantEmpire.Application.Features.Turns.Services.EndCampaignTurnPossibleActions;
using AscendantEmpire.Application.Features.Turns.Services.TurnPipeline;
using AscendantEmpire.Application.Features.Turns.Services.TurnPipeline.Campaign.EndRound;
using AscendantEmpire.Application.Features.Turns.Services.TurnPipeline.Campaign.EndTurn;
using AscendantEmpire.BackendContracts.Turns.GetCurrentCampaignTurn;
using AscendantEmpire.Domain.Entities.Turns;
using AscendantEmpire.Domain.Enums.Turns;
using AscendantEmpire.FrontendContracts.Turns.EndCampaignTurnOrder;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

using static AscendantEmpire.Application.Common.ServiceResultBase;

namespace AscendantEmpire.Application.Features.Turns.Handlers;

public record EndCampaignTurnRequest(
    int GameId,
    int PlayerId,
    EndTurnMode EndTurnMode
) : IRequest<ServiceResult<EndCampaignTurnOutcome>>;

public record EndCampaignTurnOutcome : IEndCampaignTurnOutcome
{
    public bool TurnEnded { get; set; }
    public bool RoundEnded { get; set; }
    public int? VictoryAchievedByPlayerId { get; set; }

    /// <summary>
    /// List of Pending Actions which the Player might want to do before the end of turn. e.g. Move units, select research.
    /// </summary>
    public List<PossibleAction> PossibleActions { get; set; } = [];

    /// <summary>
    /// The contract's view of <see cref="PossibleActions"/>.
    /// </summary>
    /// <remarks>
    /// Implemented explicitly so the serialized payload keeps the concrete list above. The interface is here to describe the wire shape to callers,
    /// not to reshape what goes over it.
    /// </remarks>
    IReadOnlyCollection<IPossibleAction> IEndCampaignTurnOutcome.PossibleActions => PossibleActions;
}

internal class EndCampaignTurnRequestValidator : Validator<EndCampaignTurnRequest>
{
    public EndCampaignTurnRequestValidator()
    {
        RuleFor(x => x.GameId)
            .GreaterThan(0).WithMessage("Game Id is required.");

        RuleFor(x => x.PlayerId)
            .GreaterThan(0).WithMessage("Player Id is required.");

        RuleFor(x => x.EndTurnMode)
            .IsInEnum().WithMessage("End Turn Mode is not valid.");
    }
}

internal class EndCampaignTurnHandler(
    ILogger<EndCampaignTurnHandler> logger,
    IDbContextFactory<AscendantEmpireContext> dbFactory,
    IEndCampaignTurnPossibleActionService possibleActionService,
    ITurnServicePipeline<IEndCampaignTurnProcess> endCampaignTurnPipeline,
    ITurnServicePipeline<IEndCampaignRoundProcess> endCampaignRoundPipeline,
    ICampaignTurnService campaignTurnService,
    ICampaignRoundService campaignRoundService,
    IPlayerTurnService playerTurnService,
    ICampaignTurnAccessService campaignTurnAccessService) : IRequestHandler<EndCampaignTurnRequest, ServiceResult<EndCampaignTurnOutcome>>
{

    public async Task<ServiceResult<EndCampaignTurnOutcome>> Handle(EndCampaignTurnRequest request, CancellationToken cancelToken)
    {
        try
        {
            // Validate the request object
            var validateRequest = new EndCampaignTurnRequestValidator();
            ValidationResult validationResult = validateRequest.Validate(request);

            if (validationResult.IsValid == false)
            {
                IEnumerable<string> errors = validationResult.Errors.Select(e => e.ErrorMessage);
                string error = $"Validation failed for {nameof(EndCampaignTurnRequest)}.";
                logger.LogWarning("{Error} Errors: {Errors}", error, string.Join("; ", errors));

                return ServiceResult<EndCampaignTurnOutcome>.Fail(errors.Prepend(error));
            }

            // Validate
            //      the Game is active,
            //      the Player is a part of it,
            //      it's the Player's turn.
            ServiceResult<CurrentCampaignTurn> currentTurnResult = await ValidateGameAndPlayerAsync(request.GameId, request.PlayerId, cancelToken);
            if (currentTurnResult.HasSucceeded == false || currentTurnResult.SuccessValue is null)
            {
                return ServiceResult<EndCampaignTurnOutcome>.Fail(currentTurnResult.Errors);
            }

            CurrentCampaignTurn currentTurn = currentTurnResult.SuccessValue;

            await using AscendantEmpireContext context = await dbFactory.CreateDbContextAsync(cancelToken);

            // Return the Turn Queue and Queue Participant records
            ServiceResult<DTQueueInfo> queueInfoResult = await GetQueueInformationAsync(context, currentTurn, cancelToken);
            if (queueInfoResult.HasSucceeded == false || queueInfoResult.SuccessValue is null)
            {
                return ServiceResult<EndCampaignTurnOutcome>.Fail(queueInfoResult.Errors);
            }

            DTQueueInfo queueInfo = queueInfoResult.SuccessValue;

            // Developer_AllAboutMe is meant to skip every other Player's Turn and come straight back round to this one. Nothing implements that yet, so
            // say so plainly rather than quietly ending a single Turn, which is not what the caller asked for.
            if (request.EndTurnMode == EndTurnMode.Developer_AllAboutMe)
            {
                const string error = "The developer end turn mode is not implemented.";
                logger.LogWarning("{Error} Game Id {GameId}, Player Id {PlayerId}.", error, request.GameId, request.PlayerId);

                return ServiceResult<EndCampaignTurnOutcome>.Fail(error);
            }

            // At this point the Turn can technically be ended, but if this is the first request to end it we check whether there is anything outstanding
            // the Player *could* still do. Finding something is not a refusal: it comes back so the client can offer the Player the chance to deal with
            // it, and the Player ends the Turn for real by asking again with EndTurnMode.ForceEnd.
            if (request.EndTurnMode == EndTurnMode.CheckForActions)
            {
                var possibleActionContext = new PossibleActionContext(request.GameId, request.PlayerId, currentTurn.CurrentRoundNumber);
                IReadOnlyList<PossibleAction> possibleActions = await possibleActionService.GetPossibleActionsAsync(possibleActionContext, cancelToken);

                if (possibleActions.Count > 0)
                {
                    // Inform the player of outstanding actions without ending the turn.
                    return ServiceResult<EndCampaignTurnOutcome>.Succeed(new EndCampaignTurnOutcome
                    {
                        TurnEnded = false,
                        RoundEnded = false,
                        PossibleActions = [.. possibleActions]
                    });
                }

                // Nothing outstanding, so there is nothing to prompt about and the check becomes the end of the Turn, which is exactly what
                // EndTurnMode.CheckForActions promises. Fall through and end it.
            }

            // TODO: This needs a bit of a change. You might move some units,
            //      then hit 'end turn' which triggers your units with pre-arranged orders to move/do actions.
            //      then some of those units may have remaining movement points, so you have to let the user move them as well.
            //
            //      At the start of the turn you have a list of units to move. Then after the auto-actions, you might have more to move.
            return await EndTurnAndStartNextAsync(context, currentTurn, queueInfo, cancelToken);
        }
        catch (Exception ex)
        {
            string error = $"An error occurred while processing {nameof(EndCampaignTurnRequest)}.";
            logger.LogError(ex, "{Error}", error);
            return ServiceResult<EndCampaignTurnOutcome>.Fail(error);
        }
    }

    /// <summary>
    /// Ends the current Player's Turn and hands play on to whoever goes next.
    /// </summary>
    /// <remarks>
    /// <para>Mid-Round that means starting the next Player's Turn. On the last Turn of a Round it means closing the Round, opening the next one, and
    /// starting the Turn of whoever leads it.</para>
    /// <para>Every step is guarded, because a half-applied hand-over is worse than a refused one: if the Turn cannot be closed, or the Player who
    /// should play next cannot be resolved, the call fails rather than leaving the Queue with nobody active and the Game unplayable.</para>
    /// </remarks>
    /// <param name="context">The database context used to persist the hand-over.</param>
    /// <param name="currentTurn">The Turn being ended.</param>
    /// <param name="queueInfo">The Turn Queue and Participant records behind that Turn.</param>
    /// <param name="cancelToken">Cancellation token.</param>
    private async Task<ServiceResult<EndCampaignTurnOutcome>> EndTurnAndStartNextAsync(
        AscendantEmpireContext context,
        CurrentCampaignTurn currentTurn,
        DTQueueInfo queueInfo,
        CancellationToken cancelToken)
    {
        // Start the End Turn pipeline processing, then stamp the Participant and Queue records.
        ServiceResultBase endTurnResult = await EndTurnAsync(context, currentTurn);

        if (endTurnResult.HasSucceeded == false)
        {
            return ServiceResult<EndCampaignTurnOutcome>.Fail(endTurnResult.Errors);
        }

        bool lastTurnInRound = queueInfo.CurrentParticipant.TurnOrder == queueInfo.GreatestTurnOrder;

        if (lastTurnInRound == false)
        { // Not the last turn in the Round, so start the next Turn.
            ServiceResult<CurrentCampaignTurn> nextTurnResult = await campaignTurnService.GetNextCampaignTurnAsync(currentTurn.GameId,
                turnOrderOfLastPlayer: queueInfo.CurrentParticipant.TurnOrder,
                cancelToken);

            if (nextTurnResult.HasSucceeded == false || nextTurnResult.SuccessValue is null)
            {
                const string error = "An error occurred while retrieving the next Campaign turn.";
                logger.LogError("{Error} Errors: {Errors}", error, string.Join(Environment.NewLine, nextTurnResult.Errors));

                return ServiceResult<EndCampaignTurnOutcome>.Fail(error);
            }

            ServiceResultBase startNextTurnResult = await campaignRoundService.StartNextTurnAsync(context, nextTurnResult.SuccessValue);

            if (startNextTurnResult.HasSucceeded == false)
            {
                const string error = "An error occurred while starting the next Campaign turn.";
                logger.LogError("{Error} Errors: {Errors}", error, string.Join(Environment.NewLine, startNextTurnResult.Errors));

                return ServiceResult<EndCampaignTurnOutcome>.Fail(startNextTurnResult.Errors);
            }

            return ServiceResult<EndCampaignTurnOutcome>.Succeed(new EndCampaignTurnOutcome
            {
                TurnEnded = true,
                RoundEnded = false,
                PossibleActions = []
            });
        }

        // That was the last Turn in the Round, so the Round closes before anybody plays again.
        ServiceResultBase endRoundResult = await EndRoundAsync(context, currentTurn);

        if (endRoundResult.HasSucceeded == false)
        {
            return ServiceResult<EndCampaignTurnOutcome>.Fail(endRoundResult.Errors);
        }

        // Opening the Round is a complete hand-over: the service resets every active Participant to Pending, moves the Queue on, and starts
        // the Turn of whoever leads the new Round. Nothing needs to follow it here, and the same call is what starts a brand new Game.
        int nextRoundNumber = currentTurn.CurrentRoundNumber + 1;

        ServiceResultBase startRoundResult = await campaignRoundService.StartRoundAsync(context, queueInfo.TurnQueue.Id, nextRoundNumber, currentTurn.GameId);

        if (startRoundResult.HasSucceeded == false)
        {
            const string error = "An error occurred while starting the next Round.";
            logger.LogError("{Error} Errors: {Errors}", error, string.Join(Environment.NewLine, startRoundResult.Errors));

            return ServiceResult<EndCampaignTurnOutcome>.Fail(startRoundResult.Errors);
        }

        return ServiceResult<EndCampaignTurnOutcome>.Succeed(new EndCampaignTurnOutcome
        {
            TurnEnded = true,
            RoundEnded = true,
            PossibleActions = [] // Return no items here because it's a different Players turn.
        });
    }

    /// <summary>
    /// Validate the Game is active, the Player is a part of it, and that it is currently the Player's turn.
    /// </summary>
    /// <returns>Returns the current turn information if all validations pass.</returns>
    private async Task<ServiceResult<CurrentCampaignTurn>> ValidateGameAndPlayerAsync(int gameId, int playerId, CancellationToken cancelToken)
    {
        // The Game being active with this Player in it, and a Turn being open at all, are the same two checks the read-only possible-actions request
        // makes, so they are shared. Only what follows — the Turn having to be *this* Player's — is particular to ending one.
        CampaignTurnAccess access = await campaignTurnAccessService.GetCampaignTurnAccessAsync(gameId, playerId, cancelToken);

        switch (access.Status)
        {
            case CampaignTurnAccessStatus.PlayerNotInGame:
            case CampaignTurnAccessStatus.NoTurnInProgress:
                return ServiceResult<CurrentCampaignTurn>.NotFound(access.Errors);

            case CampaignTurnAccessStatus.Failed:
                return ServiceResult<CurrentCampaignTurn>.Fail(access.Errors);
        }

        CurrentCampaignTurn currentTurn = access.CurrentTurn!;

        // Confirm it is the Player's turn. A refusal here, unlike on the read-only route: a Player asking to end a Turn that is not theirs is asking for
        // something the game will not do, not merely asking a question with a dull answer.
        if (currentTurn.PlayerId != playerId)
        {
            string error = $"It is not Player Id {playerId}'s turn.";
            logger.LogWarning("{Error}", error);
            return ServiceResult<CurrentCampaignTurn>.Fail(error);
        }

        return ServiceResult<CurrentCampaignTurn>.Succeed(currentTurn);
    }

    private record DTQueueInfo
    {
        public TurnQueue TurnQueue { get; init; } = null!;
        public TurnQueueParticipant CurrentParticipant { get; init; } = null!;
        public List<TurnQueueParticipant> ActiveParticipants { get; init; } = [];
        public int LeastTurnOrder { get; init; }
        public int GreatestTurnOrder { get; init; }
    }

    private async Task<ServiceResult<DTQueueInfo>> GetQueueInformationAsync(
        AscendantEmpireContext context,
        CurrentCampaignTurn currentTurn,
        CancellationToken cancelToken)
    {
        // Get the Turn Queue record, and the Participant record for the current Player.
        var models = await context.TurnQueues
            .Where(q => q.Id == currentTurn.TurnQueueId)
            .Select(q => new
            {
                TurnQueue = q,
                CurrentParticipant = q.TurnQueueParticipants.SingleOrDefault(p => p.Id == currentTurn.TurnQueueParticipantId),
                ActiveParticipants = q.TurnQueueParticipants.Where(p => p.IsActiveInLane).ToList()
            })
            .SingleOrDefaultAsync(cancelToken);

        if (models is null)
        {
            return ServiceResult<DTQueueInfo>.NotFound("Turn queue was not found.");
        }

        if (models.CurrentParticipant is null)
        {
            return ServiceResult<DTQueueInfo>.NotFound("Current turn queue participant was not found.");
        }

        // Every Turn Queue has at least the Participant we just found, so the aggregates below always have a row to work on.
        return ServiceResult<DTQueueInfo>.Succeed(new DTQueueInfo
        {
            TurnQueue = models.TurnQueue,
            CurrentParticipant = models.CurrentParticipant,
            ActiveParticipants = models.ActiveParticipants,
            LeastTurnOrder = models.ActiveParticipants.Min(p => p.TurnOrder),
            GreatestTurnOrder = models.ActiveParticipants.Max(p => p.TurnOrder)
        });
    }

    private async Task<ServiceResultBase> EndRoundAsync(AscendantEmpireContext context, CurrentCampaignTurn currentTurn)
    {
        try
        {
            // Update the Turn Queue record to end the Round
            DateTimeOffset now = DateTimeOffset.UtcNow;

            int updated = await context.TurnQueues
                .Where(q => q.Id == currentTurn.TurnQueueId)
                .ExecuteUpdateAsync(setters => setters
                    .SetProperty(q => q.CurrentTurnStartAt, (DateTimeOffset?)null)
                    .SetProperty(q => q.CurrentScheduledTurnEndAt, (DateTimeOffset?)null)
                    .SetProperty(q => q.CurrentActualTurnEndAt, (DateTimeOffset?)null)
                    .SetProperty(q => q.UpdatedAt, now),
                    CancellationToken.None);

            if (updated == 0)
            {
                logger.LogWarning("No Turn Queue record was updated for End of Round, Turn Queue Id {TurnQueueId}.", currentTurn.TurnQueueId);
                return ServiceResult.Fail("Failed to update Turn Queue record to end the Round.");
            }

            // Then process the End Round pipeline
            var endRoundContext = new TurnPipelineContextData(TurnPipelineType.EndCampaignRound, currentTurn.GameId, currentTurn.CurrentRoundNumber);
            ServiceResultBase endRoundResult = await endCampaignRoundPipeline.ProcessPipelineAsync(endRoundContext);

            if (endRoundResult.HasSucceeded == false)
            {
                string error = $"An error occurred during end round pipeline processing. Errors: {string.Join("; ", endRoundResult.Errors)}";
                logger.LogError("{Error}", error);
                return ServiceResultBase.Fail(error);
            }
        }
        catch (Exception e)
        {
            string error = $"An exception occurred during End Round processing for Game Id {currentTurn.GameId}, Round Number {currentTurn.CurrentRoundNumber}.";
            logger.LogError(e, "{Error}", error);
            return ServiceResultBase.Fail(error);
        }

        return ServiceResultBase.Succeed();
    }

    /// <summary>
    /// Processes the end-of-turn operations for the specified campaign turn asynchronously.
    /// </summary>
    /// <remarks>This method coordinates the end turn pipeline and updates the database accordingly. If any
    /// step fails, the result will indicate failure and include error details.</remarks>
    /// <param name="context">The database context used to persist changes related to the end of the turn.</param>
    /// <param name="currentTurn">The current campaign turn to be ended.</param>
    /// <returns>A task that represents the asynchronous operation. The task result contains a ServiceResultBase indicating
    /// whether the end turn process succeeded or failed, including any error information.</returns>
    private async Task<ServiceResultBase> EndTurnAsync(AscendantEmpireContext context, CurrentCampaignTurn currentTurn)
    {
        try
        {
            // Every Turn is opened before its start-of-turn pipeline runs, so the Turn being ended always has a row. Refuse before anything is written if it does
            // not: the end-of-turn pipeline and the close below both act on that row, and ending the Turn without it would lose it from the record.
            if (currentTurn.PlayerTurnId is not int playerTurnId)
            {
                string missingTurnError = $"Turn Queue Id {currentTurn.TurnQueueId} in Game Id {currentTurn.GameId} has no open Player Turn for Player Id {currentTurn.PlayerId}, so the Turn cannot be ended.";
                logger.LogError("{Error}", missingTurnError);

                return ServiceResultBase.Fail(missingTurnError);
            }

            // Start the End Turn pipeline processing
            var endTurnContext = new TurnPipelineContextData(TurnPipelineType.EndCampaignTurn, currentTurn.GameId, currentTurn.PlayerId, currentTurn.CurrentRoundNumber)
            {
                PlayerTurnId = playerTurnId
            };

            ServiceResultBase endTurnResult = await endCampaignTurnPipeline.ProcessPipelineAsync(endTurnContext);

            if (endTurnResult.HasSucceeded == false)
            {
                string error = $"An error occurred during end turn pipeline processing. Errors: {string.Join("; ", endTurnResult.Errors)}";
                logger.LogError("{Error}", error);
                return ServiceResultBase.Fail(error);
            }

            // Update the Turn Queue Participant record to end the turn
            DateTimeOffset now = DateTimeOffset.UtcNow;
            int updated = await context.TurnQueueParticipants
                .Where(x => x.Id == currentTurn.TurnQueueParticipantId)
                .ExecuteUpdateAsync(setters => setters
                    .SetProperty(x => x.Status, TurnQueueParticipantStatus.TurnCompleted_Manual)
                    .SetProperty(x => x.LastCompletedRoundNumber, currentTurn.CurrentRoundNumber)
                    .SetProperty(x => x.LastTurnEndedAt, now),
                    CancellationToken.None);

            if (updated == 0)
            {
                logger.LogWarning("No Turn Queue Participant record was updated for Participant Id {ParticipantId}.", currentTurn.TurnQueueParticipantId);
                return ServiceResult.Fail("Failed to update Turn Queue Participant record to end the turn.");
            }

            // Update the Turn Queue record to end the turn
            updated = await context.TurnQueues
                .Where(q => q.Id == currentTurn.TurnQueueId)
                .ExecuteUpdateAsync(setters => setters
                    .SetProperty(q => q.CurrentTurnStartAt, (DateTimeOffset?)null)
                    .SetProperty(q => q.CurrentActualTurnEndAt, now)
                    .SetProperty(q => q.UpdatedAt, now),
                    CancellationToken.None);

            if (updated == 0)
            {
                logger.LogWarning("No Turn Queue record was updated for Turn Queue Id {TurnQueueId}.", currentTurn.TurnQueueId);
                return ServiceResult.Fail("Failed to update Turn Queue record to end the turn.");
            }

            // Closed last, after the pipeline and after the Queue clock.
            ServiceResultBase closeTurnResult = await playerTurnService.ClosePlayerTurnAsync(context, currentTurn.TurnQueueId, currentTurn.PlayerId);

            if (closeTurnResult.HasSucceeded == false)
            {
                string closeError = $"Failed to close the Player Turn for Turn Queue Id {currentTurn.TurnQueueId}, Player Id {currentTurn.PlayerId}.";
                logger.LogError("{Error} Errors: {Errors}", closeError, string.Join(Environment.NewLine, closeTurnResult.Errors));

                return ServiceResultBase.Fail(closeTurnResult.Errors);
            }
        }
        catch (Exception e)
        {
            string error = $"An exception occurred during end turn processing for Game Id {currentTurn.GameId}, Turn Queue Id {currentTurn.TurnQueueId}, Participant Id {currentTurn.TurnQueueParticipantId}.";
            logger.LogError(e, "{Error}", error);

            return ServiceResultBase.Fail(error);
        }

        return ServiceResultBase.Succeed();
    }

    private async Task<ServiceResult<int?>> IsVictoryAchievedAsync()
    {
        // TODO: Implement victory condition checks here.
        return await Task.FromResult(ServiceResult<int?>.Succeed(null));
    }
}
