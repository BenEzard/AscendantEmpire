using AscendantEmpire.Application.Common;
using AscendantEmpire.Application.Features.TroopOrders.ContextActions;
using AscendantEmpire.BackendContracts.TroopOrders.GetTroopContextActions;

using FluentValidation;

using MediatR;

using Microsoft.Extensions.Logging;

namespace AscendantEmpire.Application.Features.TroopOrders.Handlers;

/// <summary>
/// A request for the context actions available to a selected troop right now.
/// </summary>
/// <remarks>The game and player come from the caller's token, so a player cannot enumerate the actions of someone else's troops.</remarks>
public record GetTroopContextActionsRequest(
    int GameId,
    int PlayerId,
    int UnitInstanceId
) : IRequest<ServiceResultBase.ServiceResult<IReadOnlyCollection<TroopContextAction>>>;

internal class GetTroopContextActionsRequestValidator : AbstractValidator<GetTroopContextActionsRequest>
{
    public GetTroopContextActionsRequestValidator()
    {
        RuleFor(x => x.GameId)
            .GreaterThan(0).WithMessage("Game Id is required.");

        RuleFor(x => x.PlayerId)
            .GreaterThan(0).WithMessage("Player Id is required.");

        RuleFor(x => x.UnitInstanceId)
            .GreaterThan(0).WithMessage("Troop Instance Id is required.");
    }
}

internal class GetTroopContextActionsHandler(
    ILogger<GetTroopContextActionsHandler> logger,
    ITroopContextActionService contextActionService
) : IRequestHandler<GetTroopContextActionsRequest, ServiceResultBase.ServiceResult<IReadOnlyCollection<TroopContextAction>>>
{
    public async Task<ServiceResultBase.ServiceResult<IReadOnlyCollection<TroopContextAction>>> Handle(
        GetTroopContextActionsRequest request,
        CancellationToken cancelToken)
    {
        try
        {
            var validator = new GetTroopContextActionsRequestValidator();
            FluentValidation.Results.ValidationResult validationResult = validator.Validate(request);

            if (!validationResult.IsValid)
            {
                IEnumerable<string> errors = validationResult.Errors.Select(e => e.ErrorMessage);
                string error = $"Validation failed for {nameof(GetTroopContextActionsRequest)}.";
                logger.LogWarning("{Error} Errors: {Errors}", error, string.Join("; ", errors));

                return ServiceResultBase.ServiceResult<IReadOnlyCollection<TroopContextAction>>.Fail(errors.Prepend(error));
            }

            return await contextActionService.GetAvailableActionsAsync(
                request.GameId,
                request.PlayerId,
                request.UnitInstanceId,
                cancelToken);
        }
        catch (Exception ex)
        {
            const string error = "An exception occurred while resolving the context actions for a Troop.";

            logger.LogError(ex, error + " GameId: {GameId}, PlayerId: {PlayerId}, UnitInstanceId: {UnitInstanceId}.",
                request.GameId,
                request.PlayerId,
                request.UnitInstanceId);

            return ServiceResultBase.ServiceResult<IReadOnlyCollection<TroopContextAction>>.Fail(error);
        }
    }
}
