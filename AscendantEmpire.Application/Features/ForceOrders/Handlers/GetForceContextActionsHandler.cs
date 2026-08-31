using AscendantEmpire.Application.Common;
using AscendantEmpire.Application.Features.ForceOrders.ContextActions;
using AscendantEmpire.BackendContracts.ForceOrders.GetForceContextActions;

using FluentValidation;

using MediatR;

using Microsoft.Extensions.Logging;

namespace AscendantEmpire.Application.Features.ForceOrders.Handlers;

/// <summary>
/// A request for the context actions available to a selected Force right now.
/// </summary>
/// <remarks>The game and player come from the caller's token, so a player cannot enumerate the actions of someone else's Forces.</remarks>
public record GetForceContextActionsRequest(
    int GameId,
    int PlayerId,
    int UnitInstanceId
) : IRequest<ServiceResultBase.ServiceResult<IReadOnlyCollection<ForceContextAction>>>;

internal class GetForceContextActionsRequestValidator : AbstractValidator<GetForceContextActionsRequest>
{
    public GetForceContextActionsRequestValidator()
    {
        RuleFor(x => x.GameId)
            .GreaterThan(0).WithMessage("Game Id is required.");

        RuleFor(x => x.PlayerId)
            .GreaterThan(0).WithMessage("Player Id is required.");

        RuleFor(x => x.UnitInstanceId)
            .GreaterThan(0).WithMessage("Unit Instance Id is required.");
    }
}

internal class GetForceContextActionsHandler(
    ILogger<GetForceContextActionsHandler> logger,
    IForceContextActionService contextActionService
) : IRequestHandler<GetForceContextActionsRequest, ServiceResultBase.ServiceResult<IReadOnlyCollection<ForceContextAction>>>
{
    public async Task<ServiceResultBase.ServiceResult<IReadOnlyCollection<ForceContextAction>>> Handle(
        GetForceContextActionsRequest request,
        CancellationToken cancelToken)
    {
        try
        {
            var validator = new GetForceContextActionsRequestValidator();
            FluentValidation.Results.ValidationResult validationResult = validator.Validate(request);

            if (!validationResult.IsValid)
            {
                IEnumerable<string> errors = validationResult.Errors.Select(e => e.ErrorMessage);
                string error = $"Validation failed for {nameof(GetForceContextActionsRequest)}.";
                logger.LogWarning("{Error} Errors: {Errors}", error, string.Join("; ", errors));

                return ServiceResultBase.ServiceResult<IReadOnlyCollection<ForceContextAction>>.Fail(errors.Prepend(error));
            }

            return await contextActionService.GetAvailableActionsAsync(
                request.GameId,
                request.PlayerId,
                request.UnitInstanceId,
                cancelToken);
        }
        catch (Exception ex)
        {
            const string error = "An exception occurred while resolving the context actions for a Force.";

            logger.LogError(ex, error + " GameId: {GameId}, PlayerId: {PlayerId}, UnitInstanceId: {UnitInstanceId}.",
                request.GameId,
                request.PlayerId,
                request.UnitInstanceId);

            return ServiceResultBase.ServiceResult<IReadOnlyCollection<ForceContextAction>>.Fail(error);
        }
    }
}
