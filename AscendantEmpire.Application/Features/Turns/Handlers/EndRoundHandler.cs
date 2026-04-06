using AscendantEmpire.Application.Common;
using AscendantEmpire.BackendContracts.Turns.AddCampaignTurnsToRoster;

using FluentValidation;

using MediatR;

using Microsoft.Extensions.Logging;

using static AscendantEmpire.Application.Common.ServiceResultBase;

namespace AscendantEmpire.Application.Features.Turns.Handlers;

public record EndRoundRequest(
    int GameId
) : IRequest<ServiceResultBase>;

internal class EndRoundRequestValidator : AbstractValidator<EndRoundRequest>
{
    public EndRoundRequestValidator()
    {
        RuleFor(x => x.GameId)
            .GreaterThan(0).WithMessage("Game Id is required.");
    }
}

internal class EndRoundHandler(
    ILogger<EndRoundHandler> logger,
    IMediator mediator
) : IRequestHandler<EndRoundRequest, ServiceResultBase>
{
    public async Task<ServiceResultBase> Handle(EndRoundRequest request, CancellationToken cancelToken)
    {
        try
        {
            // Validate the request object
            var validator = new EndRoundRequestValidator();
            
            if (validator.Validate(request) is not { IsValid: true })
            {
                var errors = validator.Validate(request).Errors.Select(e => e.ErrorMessage);
                string error = $"Validation failed for {nameof(EndRoundRequest)}.";
                logger.LogWarning("{Error} Errors: {Errors}", error, string.Join("; ", errors));
                return Fail(errors.Prepend(error));
            }

            // Create the next Round's Turn Roster
            ServiceResult<FirstPlayerInRound> createRosterResult = await mediator.Send(new AddCampaignTurnsToRosterRequest(GameId: request.GameId), CancellationToken.None);

            if (createRosterResult.HasSucceeded == false)
            {
                string error = $"Failed to create new Turn Roster for next Round.";
                logger.LogWarning("{method}: {Error} Errors: {Errors}", nameof(EndRoundHandler), error, string.Join("; ", createRosterResult.Errors));
                return Fail(createRosterResult.Errors.Prepend(error));
            }

            FirstPlayerInRound firstPlayerInRound = createRosterResult.SuccessValue!;

            // Start the next Player's turn
            ServiceResultBase startNextTurnResult = await mediator.Send(new StartTurnOnRosterRequest(
                request.GameId,
                firstPlayerInRound.PlayerId,
                firstPlayerInRound.RoundNumber
            ), CancellationToken.None);

            if (startNextTurnResult.HasSucceeded == false)
            {
                string error = $"Failed to start the next Player's turn for Round {firstPlayerInRound.RoundNumber}.";
                logger.LogWarning("{method}: {Error} Errors: {Errors}", nameof(EndRoundHandler), error, string.Join("; ", startNextTurnResult.Errors));
                return Fail(startNextTurnResult.Errors.Prepend(error));
            }

            return Succeed();
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "An exception occurred while processing {method}", nameof(EndRoundRequest));
            return Fail("An unhandled exception occurred.");
        }
    }
}
