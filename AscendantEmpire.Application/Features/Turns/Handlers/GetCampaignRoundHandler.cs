using AscendantEmpire.Application.Common.Messaging;
using AscendantEmpire.Application.Common.Validation;
using AscendantEmpire.Application.Data;
using AscendantEmpire.Application.Features.Turns.Services;
using AscendantEmpire.BackendContracts.Turns.GetCampaignRoundInfo;
using AscendantEmpire.Domain.Enums.Turns;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

using static AscendantEmpire.Application.Common.ServiceResultBase;

namespace AscendantEmpire.Application.Features.Turns.Handlers;

public record GetCampaignRoundRequest(
    int GameId
) : IRequest<ServiceResult<CampaignRound>>;

internal class GetCampaignRoundInfoRequestValidator : Validator<GetCampaignRoundRequest>
{
    public GetCampaignRoundInfoRequestValidator()
    {
        RuleFor(x => x.GameId)
            .GreaterThan(0)
            .WithMessage("Game Id is required.");
    }
}

internal class GetCampaignRoundHandler(
    ILogger<GetCampaignRoundHandler> logger,
    IDbContextFactory<AscendantEmpireContext> dbFactory
) : IRequestHandler<GetCampaignRoundRequest, ServiceResult<CampaignRound>>
{
    public async Task<ServiceResult<CampaignRound>> Handle(
        GetCampaignRoundRequest request,
        CancellationToken cancelToken)
    {
        try
        {
            var validator = new GetCampaignRoundInfoRequestValidator();
            var validationResult = validator.Validate(request);

            if (validationResult.IsValid == false)
            {
                IEnumerable<string> errors = validationResult.Errors.Select(e => e.ErrorMessage);
                string error = $"Validation failed for {nameof(GetCampaignRoundRequest)}.";

                logger.LogWarning("{Error} Errors: {Errors}", error, string.Join("; ", errors));

                return ServiceResult<CampaignRound>.Fail(errors.Prepend(error));
            }

            await using AscendantEmpireContext context = await dbFactory.CreateDbContextAsync(cancelToken);

            CampaignRound? round = await CampaignTurnQueueQuery.Active(context, request.GameId)
                .Select(x => new CampaignRound
                {
                    GameId = x.GameId,
                    TurnQueueId = x.Id,

                    CurrentRoundNumber = x.CurrentRoundNumber,

                    // The current Player is the one *taking* their Turn, not the next one waiting: a Round leaves everybody Pending
                    // except the Player whose Turn it is, so reading Pending here would name whoever plays next. Every other reader of
                    // "whose turn is it" - GetCurrentCampaignTurnAsync in particular - agrees on TakingTurn, and they must not disagree.
                    //
                    // Exactly one Participant should ever hold that status, so the ordering below is a defensive tiebreak rather than a
                    // selection: it keeps the answer stable rather than arbitrary if a half-applied hand-over ever leaves two.
                    CurrentPlayerId = x.TurnQueueParticipants
                        .Where(p => p.IsActiveInLane)
                        .Where(p => p.Status == TurnQueueParticipantStatus.TakingTurn)
                        .OrderBy(p => p.TurnOrder)
                        .Select(p => (int?)p.PlayerId)
                        .FirstOrDefault(),

                    CurrentPlayerTurnOrder = x.TurnQueueParticipants
                        .Where(p => p.IsActiveInLane)
                        .Where(p => p.Status == TurnQueueParticipantStatus.TakingTurn)
                        .OrderBy(p => p.TurnOrder)
                        .Select(p => (int?)p.TurnOrder)
                        .FirstOrDefault(),

                    TurnStartTime = x.CurrentTurnStartAt,
                    ScheduledTurnEndTime = x.CurrentScheduledTurnEndAt,
                    ActualTurnEndTime = x.CurrentActualTurnEndAt,

                    IsCompleted = x.IsCompleted
                })
                .SingleOrDefaultAsync(cancelToken);

            if (round == null)
            {
                const string error = "No active Campaign Turn Queue found for Game.";

                logger.LogWarning("{Error} Game Id: {GameId}", error, request.GameId);

                return ServiceResult<CampaignRound>.NotFound(error);
            }

            return ServiceResult<CampaignRound>.Succeed(round);
        }
        catch (InvalidOperationException ex)
        {
            const string error = "Multiple active Campaign Turn Queues were found for Game.";

            logger.LogError(ex, "{Error} Game Id: {GameId}", error, request.GameId);

            return ServiceResult<CampaignRound>.Fail(error);
        }
    }
}