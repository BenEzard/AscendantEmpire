using AscendantEmpire.Application.Common;
using AscendantEmpire.Application.Data;
using AscendantEmpire.Domain.Entities.Turns;
using AscendantEmpire.Domain.Enums.Turns;

using FluentValidation;

using MediatR;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace AscendantEmpire.Application.Features.Turns.Handlers;

public record StartCampaignRoundRequest(
    int GameId
) : IRequest<ServiceResultBase>;

internal class StartCampaignRoundRequestValidator : AbstractValidator<StartCampaignRoundRequest>
{
    public StartCampaignRoundRequestValidator()
    {
        RuleFor(x => x.GameId)
            .GreaterThan(0).WithMessage("Game Id is required.");
    }
}

internal class StartCampaignRoundHandler(
    ILogger<StartCampaignRoundHandler> logger,
    IDbContextFactory<AscendantEmpireContext> dbFactory
) : IRequestHandler<StartCampaignRoundRequest, ServiceResultBase>
{
    public async Task<ServiceResultBase> Handle(StartCampaignRoundRequest request, CancellationToken cancelToken)
    {
        try
        {
            // Validate the request object
            var validator = new StartCampaignRoundRequestValidator();

            if (validator.Validate(request) is not { IsValid: true })
            {
                var errors = validator.Validate(request).Errors.Select(e => e.ErrorMessage);
                string error = $"Validation failed for {nameof(StartCampaignRoundRequest)}.";
                logger.LogWarning("{Error} Errors: {Errors}", error, string.Join("; ", errors));
                return ServiceResultBase.Fail(errors.Prepend(error));
            }

            await using AscendantEmpireContext context = await dbFactory.CreateDbContextAsync(cancelToken);

            // Update the Turn Queue for the next Campaign Round
            TurnQueue? turnQueue = context.TurnQueues
                .Where(tq => tq.GameId == request.GameId)
                .Where(tq => tq.QueueType == TurnQueueType.Campaign)
                .Where(tq => tq.IsCompleted == false)
                .FirstOrDefault();

            if (turnQueue == null)
            {
                logger.LogWarning("No active TurnQueue found for GameId {GameId} when processing StartCampaignRoundRequest.", request.GameId);
                return ServiceResultBase.Fail("No active turn queue found for the specified game.");
            }

            turnQueue.CurrentRoundNumber++;
            turnQueue.UpdatedAt = DateTime.UtcNow;

            await context.SaveChangesAsync(CancellationToken.None);

            // Update the Queue Participants for the next Campaign Round
            var participants = context.TurnQueueParticipants
                .Where(tqp => tqp.TurnQueueId == turnQueue.Id)
                .ExecuteUpdate(setters => setters
                    .SetProperty(tqp => tqp.Status, TurnQueueParticipantStatus.Pending));

            // TODO: Start Campaign Turn for first Player
           
            return ServiceResultBase.Succeed();
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "An exception occurred while processing StartCampaignRoundRequest.");
            return ServiceResultBase.Fail("An internal error occurred while processing the request.");
        }
    }
}
