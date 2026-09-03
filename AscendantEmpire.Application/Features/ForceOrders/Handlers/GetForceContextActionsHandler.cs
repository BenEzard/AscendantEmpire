using AscendantEmpire.Application.Common;
using AscendantEmpire.Application.Common.Validation;
using AscendantEmpire.Application.Features.ForceOrders.ContextActions;
using AscendantEmpire.BackendContracts.ForceOrders.GetForceContextActions;

using MediatR;

using Microsoft.Extensions.Logging;

namespace AscendantEmpire.Application.Features.ForceOrders.Handlers;

/// <summary>
/// A request for the context actions available to the player's current Force selection right now.
/// </summary>
/// <remarks>
/// <para>The game and player come from the caller's token, so a player cannot enumerate the actions of someone else's Forces.</para>
/// <para>
/// <c>ForceIds</c> holds the whole selection: an action offered over several Forces is one order covering all of them, so it is judged against
/// every one of them. Read the ids as Force ids — <c>UnitInstance</c> ids today, Command ids too once Commands are fielded.
/// </para>
/// </remarks>
public record GetForceContextActionsRequest(
    int GameId,
    int PlayerId,
    IReadOnlyCollection<int> ForceIds
) : IRequest<ServiceResultBase.ServiceResult<IReadOnlyCollection<ForceContextAction>>>;

internal class GetForceContextActionsRequestValidator : Validator<GetForceContextActionsRequest>
{
    public GetForceContextActionsRequestValidator()
    {
        RuleFor(x => x.GameId)
            .GreaterThan(0).WithMessage("Game Id is required.");

        RuleFor(x => x.PlayerId)
            .GreaterThan(0).WithMessage("Player Id is required.");

        RuleFor(x => x.ForceIds)
            .NotNull().WithMessage("At least one Force Id is required.")
            .Must(forceIds => forceIds != null && forceIds.Count > 0).WithMessage("At least one Force Id is required.");

        RuleForEach(x => x.ForceIds)
            .GreaterThan(0).WithMessage("Force Id is required.");
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
            ValidationResult validationResult = validator.Validate(request);

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
                request.ForceIds,
                cancelToken);
        }
        catch (Exception ex)
        {
            const string error = "An exception occurred while resolving the context actions for a Force selection.";

            logger.LogError(ex, error + " GameId: {GameId}, PlayerId: {PlayerId}, ForceIds: {ForceIds}.",
                request.GameId,
                request.PlayerId,
                string.Join(", ", request.ForceIds ?? []));

            return ServiceResultBase.ServiceResult<IReadOnlyCollection<ForceContextAction>>.Fail(error);
        }
    }
}
