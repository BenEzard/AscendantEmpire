using AscendantEmpire.Application.Common.Messaging;
using AscendantEmpire.Application.Common.Validation;
using AscendantEmpire.Application.Features.ForceOrders.ContextActions;
using AscendantEmpire.BackendContracts.ForceOrders.SplitForce;

using Microsoft.Extensions.Logging;

using static AscendantEmpire.Application.Common.ServiceResultBase;

namespace AscendantEmpire.Application.Features.ForceOrders.Handlers;

/// <summary>
/// A request for the numbers the split dialog needs to offer a legal division of one unled Force.
/// </summary>
/// <remarks>
/// <para>
/// The game and player come from the caller's token, so a player cannot read the split options of someone else's Forces.
/// </para>
/// <para>
/// One Force, not a selection — unlike the context-actions request. One dialog carves one Force: a selection of several would need a tree per Force and a
/// confirmation per Force, which is not a thing the design asks for.
/// </para>
/// </remarks>
public record GetSplitForceOptionsRequest(
    int GameId,
    int PlayerId,
    int UnitInstanceId
) : IRequest<ServiceResult<SplitForceOptions>>;

internal class GetSplitForceOptionsRequestValidator : Validator<GetSplitForceOptionsRequest>
{
    public GetSplitForceOptionsRequestValidator()
    {
        RuleFor(x => x.GameId)
            .GreaterThan(0).WithMessage("Game Id is required.");

        RuleFor(x => x.PlayerId)
            .GreaterThan(0).WithMessage("Player Id is required.");

        RuleFor(x => x.UnitInstanceId)
            .GreaterThan(0).WithMessage("Unit Instance Id is required.");
    }
}

internal class GetSplitForceOptionsHandler(
    ILogger<GetSplitForceOptionsHandler> logger,
    IForceActionContextBuilder contextBuilder
) : IRequestHandler<GetSplitForceOptionsRequest, ServiceResult<SplitForceOptions>>
{
    public async Task<ServiceResult<SplitForceOptions>> Handle(GetSplitForceOptionsRequest request, CancellationToken cancelToken)
    {
        var validator = new GetSplitForceOptionsRequestValidator();
        ValidationResult validationResult = validator.Validate(request);

        if (!validationResult.IsValid)
        {
            IEnumerable<string> errors = validationResult.Errors.Select(e => e.ErrorMessage);
            string error = $"Validation failed for {nameof(GetSplitForceOptionsRequest)}.";
            logger.LogWarning("{Error} Errors: {Errors}", error, string.Join("; ", errors));

            return ServiceResult<SplitForceOptions>.Fail(errors.Prepend(error));
        }

        // Read through the same builder the toolbar's own pipeline uses, so the dialog's numbers and the button's verdict can never come from two
        // different views of the Force. It needs no new query: everything here is already on the context it projects.
        IReadOnlyCollection<ForceActionContext>? contexts = await contextBuilder.BuildAsync(
            request.GameId,
            request.PlayerId,
            [request.UnitInstanceId],
            cancelToken);

        ForceActionForce? force = contexts?.SingleOrDefault()?.SelectedForce;

        // The builder is all-or-nothing, and deliberately folds "no such Force", "in another game", "wiped out" and "belongs to someone else" into one
        // null. Telling them apart here would let a caller probe what another player is fielding.
        if (force is null)
        {
            string error = $"Unit Instance Id {request.UnitInstanceId} is not in this game.";
            logger.LogWarning("Player Id {PlayerId} asked for the split options of Unit Instance Id {UnitInstanceId}, which they cannot read.",
                request.PlayerId, request.UnitInstanceId);

            return ServiceResult<SplitForceOptions>.NotFound(error);
        }

        // A commissioned formation detaches a whole sub-formation under a subordinate officer, which asks for a tier and a count rather than a list of
        // sizes. This contract cannot describe that, so refuse rather than hand back numbers the dialog would misread.
        if (force.Organisation is not { IsUnled: true } organisation)
        {
            string error = $"{force.DisplayName} is not an unled force, so it cannot be split into groups this way.";
            logger.LogInformation("Split options were refused for Unit Instance Id {UnitInstanceId}: its organisation is not unled.", force.UnitInstanceId);

            return ServiceResult<SplitForceOptions>.Fail(error);
        }

        // An unled tag is authored with its sizes together, and tag-save time enforces that, so reaching here without a base size is a catalogue fault
        // rather than a player mistake. Worded exactly as SplitForceStrategy words it, so the dialog and the button it opened from cannot disagree.
        if (organisation.UnledBaseSize is not > 0)
        {
            string error = $"Organisation '{organisation.DisplayName}' is Unled but has no authored base size, so its forces cannot be split.";
            logger.LogError("Organisation tag '{TagCode}' is Unled but has no authored base size, so Unit Instance Id {UnitInstanceId} cannot be split.",
                organisation.Code, force.UnitInstanceId);

            return ServiceResult<SplitForceOptions>.Fail(error);
        }

        return ServiceResult<SplitForceOptions>.Succeed(new SplitForceOptions
        {
            UnitInstanceId = force.UnitInstanceId,
            DisplayName = force.DisplayName,
            // Non-null wherever IsUnled is true, which the check above has already established.
            LeadershipModel = organisation.LeadershipModel!.Value,
            HealthyCount = force.HealthyCount,
            InjuredCount = force.InjuredCount,
            // Healthy and injured together: the wounded travel with their group, so they are part of what a split divides.
            TotalStrength = force.TotalStrength,
            BaseSize = organisation.UnledBaseSize.Value
        });
    }
}
