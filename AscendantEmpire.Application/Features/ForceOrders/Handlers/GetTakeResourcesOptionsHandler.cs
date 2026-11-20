using AscendantEmpire.Application.Common.Messaging;
using AscendantEmpire.Application.Common.Validation;
using AscendantEmpire.Application.Features.ForceOrders.ContextActions;
using AscendantEmpire.Application.Features.ForceOrders.ContextActions.Strategies;
using AscendantEmpire.Application.Features.Maps.MapResources.Classes;
using AscendantEmpire.Application.Helpers.ScaledNumerics;
using AscendantEmpire.BackendContracts.ForceOrders.TakeResources;

using Microsoft.Extensions.Logging;

using static AscendantEmpire.Application.Common.ServiceResultBase;

namespace AscendantEmpire.Application.Features.ForceOrders.Handlers;

/// <summary>
/// A request for what the take-resources dialog needs: which MapResources the selection could take from, and what each would give them.
/// </summary>
/// <remarks>
/// <para>
/// The game and player come from the caller's token, so a player cannot read what someone else's Forces are standing on.
/// </para>
/// <para>
/// A selection, not one Force - unlike the split options. One take is one order over everything selected, and what each resource is worth depends on how
/// many healthy individuals the whole selection can put to carrying, so a per-Force read would promise the wrong figures.
/// </para>
/// </remarks>
public record GetTakeResourcesOptionsRequest(
    int GameId,
    int PlayerId,
    IReadOnlyCollection<int> ForceIds
) : IRequest<ServiceResult<TakeResourcesOptions>>;

internal class GetTakeResourcesOptionsRequestValidator : Validator<GetTakeResourcesOptionsRequest>
{
    public GetTakeResourcesOptionsRequestValidator()
    {
        RuleFor(x => x.GameId)
            .GreaterThan(0).WithMessage("Game Id is required.");

        RuleFor(x => x.PlayerId)
            .GreaterThan(0).WithMessage("Player Id is required.");

        RuleFor(x => x.ForceIds)
            .NotEmpty().WithMessage("At least one Force Id is required.");
    }
}

internal class GetTakeResourcesOptionsHandler(
    ILogger<GetTakeResourcesOptionsHandler> logger,
    IForceActionContextBuilder contextBuilder
) : IRequestHandler<GetTakeResourcesOptionsRequest, ServiceResult<TakeResourcesOptions>>
{
    public async Task<ServiceResult<TakeResourcesOptions>> Handle(GetTakeResourcesOptionsRequest request, CancellationToken cancelToken)
    {
        var validator = new GetTakeResourcesOptionsRequestValidator();
        ValidationResult validationResult = validator.Validate(request);

        if (!validationResult.IsValid)
        {
            IEnumerable<string> errors = validationResult.Errors.Select(e => e.ErrorMessage);
            string error = $"Validation failed for {nameof(GetTakeResourcesOptionsRequest)}.";
            logger.LogWarning("{Error} Errors: {Errors}", error, string.Join("; ", errors));

            return ServiceResult<TakeResourcesOptions>.Fail(errors.Prepend(error));
        }

        // Read through the same builder the toolbar's own pipeline uses, which is also where takeability is decided - so the dialog cannot offer a resource
        // the button would not have appeared for, and needs no second view of either the Forces or the tile.
        IReadOnlyCollection<ForceActionContext>? contexts = await contextBuilder.BuildAsync(
            request.GameId,
            request.PlayerId,
            request.ForceIds,
            cancelToken);

        // The builder is all-or-nothing, and deliberately folds "no such Force", "in another game", "wiped out" and "belongs to someone else" into one null.
        // Telling them apart here would let a caller probe what another player is fielding.
        if (contexts is null || contexts.Count == 0)
        {
            const string error = "Those forces are not in this game.";
            logger.LogWarning("Player Id {PlayerId} asked for the take-resources options of Force Ids {ForceIds}, which they cannot read.",
                request.PlayerId, string.Join(", ", request.ForceIds));

            return ServiceResult<TakeResourcesOptions>.NotFound(error);
        }

        // The dialog offers one tile's resources, so a selection spanning two has nothing coherent to show. The selection bar cannot produce one today;
        // refusing rather than picking a tile keeps it that way if it ever can.
        if (contexts.Select(context => context.SelectedForce.CampaignMapTileId).Distinct().Count() > 1)
        {
            const string error = "Those forces are not all on the same tile, so they cannot take from one resource together.";
            logger.LogInformation("Player Id {PlayerId} asked for take-resources options across several tiles.", request.PlayerId);

            return ServiceResult<TakeResourcesOptions>.Fail(error);
        }

        TakeResourcesTaker[] takers =
        [
            .. contexts.Select(context => new TakeResourcesTaker(
                context.SelectedForce.UnitInstanceId,
                context.SelectedForce.DisplayName,
                context.SelectedForce.HealthyCount))
        ];

        // Every selected Force stands on the one tile, so any context's view of it is the same view.
        IReadOnlyCollection<TakeableMapResource> takeable = contexts.First().TakeableMapResourcesOnTile;

        return ServiceResult<TakeResourcesOptions>.Succeed(new TakeResourcesOptions
        {
            Resources = [.. takeable.Select(resource => Describe(resource, takers))]
        });
    }

    /// <summary>
    /// What the selection would come away with from one resource.
    /// </summary>
    /// <remarks>
    /// Planned through <see cref="TakeResourcesRules.PlanShares"/> and priced through the same yield arithmetic the handler writes with, so the figure the
    /// dialog promises is the figure the order produces.
    /// </remarks>
    private static TakeResourcesOption Describe(TakeableMapResource resource, IReadOnlyCollection<TakeResourcesTaker> takers)
    {
        long totalRaw = TakeResourcesRules.PlanShares(takers, resource.QuantityRaw).Sum(share => share.UnitsTakenRaw);

        return new TakeResourcesOption
        {
            MapResourceInstanceId = resource.MapResourceInstanceId,
            Slot = resource.Slot,
            DisplayName = resource.DisplayName,
            IconPath = resource.IconPath,
            UnitsWouldTake = (int)ScaledAmount.FromRaw(totalRaw).ToWholeRoundedDown(),
            WouldGain =
            [
                .. resource.Yields
                    .Select(yield => new TakeResourcesAssetLine
                    {
                        AssetId = yield.AssetId,
                        AssetDisplayName = yield.AssetDisplayName,
                        Amount = (int)ScaledAmount
                            .FromRaw(TakeResourcesRules.CalculateAssetAmountRaw(totalRaw, yield.AmountPerUnit))
                            .ToWholeRoundedDown()
                    })
            ]
        };
    }
}
