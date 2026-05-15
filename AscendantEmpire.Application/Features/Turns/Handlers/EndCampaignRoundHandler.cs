using AscendantEmpire.Application.Common;
using AscendantEmpire.BackendContracts.Turns.AddCampaignTurnsToRoster;

using FluentValidation;

using MediatR;

using Microsoft.Extensions.Logging;

using static AscendantEmpire.Application.Common.ServiceResultBase;

namespace AscendantEmpire.Application.Features.Turns.Handlers;

public record EndCampaignRoundRequest(
    int GameId,
    int RoundNumberEnding
) : IRequest<ServiceResultBase>;

internal class EndCampaignRoundRequestValidator : AbstractValidator<EndCampaignRoundRequest>
{
    public EndCampaignRoundRequestValidator()
    {
        RuleFor(x => x.GameId)
            .GreaterThan(0).WithMessage("Game Id is required.");

        RuleFor(x => x.RoundNumberEnding)
            .GreaterThan(0).WithMessage("Round Number Ending is required.");
    }
}

internal class EndCampaignRoundHandler(
    ILogger<EndCampaignRoundHandler> logger,
    IMediator mediator
) : IRequestHandler<EndCampaignRoundRequest, ServiceResultBase>
{
    public async Task<ServiceResultBase> Handle(EndCampaignRoundRequest request, CancellationToken cancelToken)
    {
        try
        {
            // Validate the request object
            var validator = new EndCampaignRoundRequestValidator();
            
            if (validator.Validate(request) is not { IsValid: true })
            {
                var errors = validator.Validate(request).Errors.Select(e => e.ErrorMessage);
                string error = $"Validation failed for {nameof(EndCampaignRoundRequest)}.";
                logger.LogWarning("{Error} Errors: {Errors}", error, string.Join("; ", errors));
                return Fail(errors.Prepend(error));
            }

            // TODO: Anything that needs to happen at the end of a campaign round.

            if (ShouldGameContinue())
            {
                // TODO: Start next Round
                
            }

            return Succeed();
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "An exception occurred while processing {method}", nameof(EndCampaignRoundRequest));
            return Fail("An unhandled exception occurred.");
        }
    }

    private bool ShouldGameContinue() 
    {
        // TODO: Evaluate whether the game should continue or if a winner should be declared. 

        return true; 
    }
}
