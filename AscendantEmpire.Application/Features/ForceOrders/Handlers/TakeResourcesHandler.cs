using AscendantEmpire.Application.Common;
using AscendantEmpire.Application.Common.Messaging;
using AscendantEmpire.Application.Common.Validation;
using AscendantEmpire.Application.Data;
using AscendantEmpire.Application.Features.Assets.Classes;
using AscendantEmpire.Application.Features.Assets.Services;
using AscendantEmpire.Application.Features.ForceOrders.ContextActions;
using AscendantEmpire.Application.Features.ForceOrders.ContextActions.Strategies;
using AscendantEmpire.Application.Features.Maps.MapResources.Classes;
using AscendantEmpire.Application.Features.Maps.MapResources.Services;
using AscendantEmpire.Application.Features.Turns.Services;
using AscendantEmpire.Application.Helpers.ScaledNumerics;
using AscendantEmpire.BackendContracts.ForceOrders.TakeResources;
using AscendantEmpire.BackendContracts.Turns.GetCurrentCampaignTurn;
using AscendantEmpire.Domain.Entities.Assets;
using AscendantEmpire.Domain.Entities.Games;
using AscendantEmpire.Domain.Entities.Units;
using AscendantEmpire.Domain.Enums.Forces;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.Logging;

using static AscendantEmpire.Application.Common.ServiceResultBase;

namespace AscendantEmpire.Application.Features.ForceOrders.Handlers;

/// <summary>
/// A request to take one of the MapResources on a tile into the cargo of the Forces standing there.
/// </summary>
/// <remarks>
/// <para>
/// Deliberately not a <c>ForceOrder</c>. A context action resolves now; a <c>ForceOrder</c> is a standing order the turn processor advances. Taking costs
/// the Force its whole turn without ever becoming one, exactly as a split does.
/// </para>
/// </remarks>
public record TakeResourcesRequest(
    int GameId,
    int PlayerId,
    IReadOnlyCollection<int> ForceIds,
    int MapResourceInstanceId
) : IRequest<ServiceResult<TakeResourcesResult>>;

internal class TakeResourcesRequestValidator : Validator<TakeResourcesRequest>
{
    public TakeResourcesRequestValidator()
    {
        RuleFor(x => x.GameId)
            .GreaterThan(0).WithMessage("Game Id is required.");

        RuleFor(x => x.PlayerId)
            .GreaterThan(0).WithMessage("Player Id is required.");

        RuleFor(x => x.ForceIds)
            .NotEmpty().WithMessage("At least one Force Id is required.");

        RuleFor(x => x.MapResourceInstanceId)
            .GreaterThan(0).WithMessage("Map Resource Instance Id is required.");
    }
}

internal class TakeResourcesHandler(
    ILogger<TakeResourcesHandler> logger,
    IDbContextFactory<AscendantEmpireContext> dbFactory,
    ICampaignTurnService campaignTurnService,
    IForceContextActionService contextActionService,
    ITakeableMapResourceReader takeableMapResourceReader,
    IAssetBankProvisioningService assetBankProvisioningService,
    IAssetBankCargoService assetBankCargoService
) : IRequestHandler<TakeResourcesRequest, ServiceResult<TakeResourcesResult>>
{
    public async Task<ServiceResult<TakeResourcesResult>> Handle(TakeResourcesRequest request, CancellationToken cancelToken)
    {
        var validator = new TakeResourcesRequestValidator();
        ValidationResult validationResult = validator.Validate(request);

        if (!validationResult.IsValid)
        {
            IEnumerable<string> errors = validationResult.Errors.Select(e => e.ErrorMessage);
            string error = $"Validation failed for {nameof(TakeResourcesRequest)}.";
            logger.LogWarning("{Error} Errors: {Errors}", error, string.Join("; ", errors));

            return ServiceResult<TakeResourcesResult>.Fail(errors.Prepend(error));
        }

        // The same Force listed twice is one Force, and letting a duplicate through would have it load up twice off one patch.
        int[] forceIds = [.. request.ForceIds.Distinct()];

        await using AscendantEmpireContext context = await dbFactory.CreateDbContextAsync(cancelToken);

        // Tracked: their movement is about to be spent, and the tile comes with them for the coordinates on the response.
        List<UnitInstance> forces = await context.UnitInstances
            .Include(force => force.CampaignMap)
            .Where(force => force.GameId == request.GameId && forceIds.Contains(force.Id))
            .ToListAsync(cancelToken);

        if (forces.Count != forceIds.Length)
        {
            string error = $"{(forceIds.Length == 1 ? "That force is" : "Those forces are")} not all in this game.";
            logger.LogWarning("Force Ids {ForceIds} were not all found in Game Id {GameId}.", string.Join(", ", forceIds), request.GameId);

            return ServiceResult<TakeResourcesResult>.NotFound(error);
        }

        UnitInstance? foreignForce = forces.Find(force => force.PlayerId != request.PlayerId);

        if (foreignForce is not null)
        {
            string error = $"Unit Instance Id {foreignForce.Id} does not belong to you.";
            logger.LogWarning("Player Id {PlayerId} tried to take resources with Unit Instance Id {UnitInstanceId}, which belongs to Player Id {OwnerPlayerId}.",
                request.PlayerId, foreignForce.Id, foreignForce.PlayerId);

            return ServiceResult<TakeResourcesResult>.Forbidden(error);
        }

        if (forces.Select(force => force.CampaignMapTileId).Distinct().Count() > 1)
        {
            const string error = "Those forces are not all on the same tile, so they cannot take from one resource together.";
            logger.LogInformation("Player Id {PlayerId} tried to take resources with forces standing on several tiles.", request.PlayerId);

            return ServiceResult<TakeResourcesResult>.Fail(error);
        }

        ServiceResult<TakeResourcesResult>? turnRefusal = await ValidateItIsThisPlayersTurnAsync(request, cancelToken);

        if (turnRefusal is not null)
        {
            return turnRefusal;
        }

        // The rules themselves, re-run against freshly built context, so a refusal's wording is the wording the disabled button would have shown.
        ServiceResult<ForceActionAvailability> availabilityResult = await contextActionService.EvaluateActionAsync(
            request.GameId,
            request.PlayerId,
            forceIds,
            ForceActionType.TakeResources,
            cancelToken);

        if (availabilityResult.HasSucceeded == false || availabilityResult.SuccessValue is null)
        {
            const string error = "The order could not be judged against the forces' current situation.";
            logger.LogWarning("Could not evaluate {ActionType} for Force Ids {ForceIds}: {Errors}",
                ForceActionType.TakeResources, string.Join(", ", forceIds), string.Join("; ", availabilityResult.Errors));

            return ServiceResult<TakeResourcesResult>.Fail(availabilityResult.Errors.Prepend(error));
        }

        if (availabilityResult.SuccessValue.IsEnabled == false)
        {
            logger.LogInformation("Refused {ActionType} for Force Ids {ForceIds}: {Reasons}",
                ForceActionType.TakeResources, string.Join(", ", forceIds), string.Join("; ", availabilityResult.SuccessValue.DisabledReasons));

            return ServiceResult<TakeResourcesResult>.Fail(availabilityResult.SuccessValue.DisabledReasons);
        }

        int tileId = forces[0].CampaignMapTileId;

        // Read again through the one reader that decides takeability, rather than trusting what the dialog was shown: the patch may have been emptied, or
        // the resource switched off, since it opened.
        IReadOnlyDictionary<int, IReadOnlyCollection<TakeableMapResource>> takeableByTileId =
            await takeableMapResourceReader.GetForTilesAsync(context, request.GameId, request.PlayerId, [tileId], cancelToken);

        TakeableMapResource? resource = takeableByTileId.TryGetValue(tileId, out IReadOnlyCollection<TakeableMapResource>? onTile)
            ? onTile.FirstOrDefault(candidate => candidate.MapResourceInstanceId == request.MapResourceInstanceId)
            : null;

        if (resource is null)
        {
            const string error = "There is nothing of that resource left to take here.";
            logger.LogInformation(
                "Player Id {PlayerId} tried to take Map Resource Instance Id {MapResourceInstanceId} on Campaign Map Tile Id {TileId}, which is no longer takeable for them.",
                request.PlayerId, request.MapResourceInstanceId, tileId);

            return ServiceResult<TakeResourcesResult>.Fail(error);
        }

        return await TakeAsync(context, request, forces, resource, cancelToken);
    }

    /// <summary>
    /// Performs the take itself, once every refusal has been ruled out.
    /// </summary>
    /// <remarks>
    /// <para>
    /// One transaction over three kinds of change, because a half-done take is worse than none: the quantity off the MapResource, the Assets into each
    /// Force's cargo, and the movement off each Force that took. A load credited against a patch that was never charged would be stock out of nothing.
    /// </para>
    /// <para>
    /// A Force that takes nothing is left entirely alone - no bank provisioned, no movement spent. The order emptied the patch before it got there, so it
    /// has done nothing to pay for.
    /// </para>
    /// </remarks>
    private async Task<ServiceResult<TakeResourcesResult>> TakeAsync(
        AscendantEmpireContext context,
        TakeResourcesRequest request,
        List<UnitInstance> forces,
        TakeableMapResource resource,
        CancellationToken cancelToken)
    {
        int[] takingForceIds = [.. forces.Select(force => force.Id)];

        // Scalars only off the compiled template, which is what keeps this handler's own query off the EF complex types it owns - see docs/testing.md.
        Dictionary<int, string> displayNameByForceId = await context.UnitInstances
            .AsNoTracking()
            .Where(force => force.GameId == request.GameId && takingForceIds.Contains(force.Id))
            .Select(force => new { force.Id, DisplayName = force.Name ?? force.UnitTemplate.Compiled!.DisplayName })
            .ToDictionaryAsync(force => force.Id, force => force.DisplayName, cancelToken);

        TakeResourcesTaker[] takers =
        [
            .. forces.Select(force => new TakeResourcesTaker(
                force.Id,
                displayNameByForceId.TryGetValue(force.Id, out string? name) ? name : $"Force {force.Id}",
                force.HealthyCount))
        ];

        IReadOnlyList<TakeResourcesShare> shares = TakeResourcesRules.PlanShares(takers, resource.QuantityRaw);

        // Tracked, since this row is about to be written. Read by id and game, so a stale instance id from the dialog cannot charge another game's tile.
        MapResourceInstance? instance = await context.MapResourceInstances
            .FirstOrDefaultAsync(row => row.Id == resource.MapResourceInstanceId && row.GameId == request.GameId, cancelToken);

        if (instance?.Quantity is null)
        {
            const string error = "There is nothing of that resource left to take here.";
            logger.LogWarning("Map Resource Instance Id {MapResourceInstanceId} could not be charged in Game Id {GameId}.",
                resource.MapResourceInstanceId, request.GameId);

            return ServiceResult<TakeResourcesResult>.Fail(error);
        }

        await using IDbContextTransaction transaction = await context.Database.BeginTransactionAsync(cancelToken);

        Dictionary<int, UnitInstance> forceById = forces.ToDictionary(force => force.Id);
        var outcomes = new List<TakeResourcesForceOutcome>();
        var gainedByAssetId = new Dictionary<int, long>();
        long totalTakenRaw = 0;

        foreach (TakeResourcesShare share in shares)
        {
            UnitInstance force = forceById[share.Taker.UnitInstanceId];

            outcomes.Add(new TakeResourcesForceOutcome
            {
                UnitInstanceId = force.Id,
                DisplayName = share.Taker.DisplayName,
                UnitsTaken = (int)ScaledAmount.FromRaw(share.UnitsTakenRaw).ToWholeRoundedDown()
            });

            if (share.UnitsTakenRaw <= 0)
            {
                continue;
            }

            CargoCredit[] credits =
            [
                .. resource.Yields
                    .Select(yield => new CargoCredit(yield.AssetId, TakeResourcesRules.CalculateAssetAmountRaw(share.UnitsTakenRaw, yield.AmountPerUnit)))
                    .Where(credit => credit.RawQuantity > 0)
            ];

            // Cargo is provisioned on first use, so most Forces reach here without a bank. The get-or-create hands back one already staged by an earlier
            // Force in this very loop, which is what stops a second insert failing the unique index.
            ServiceResult<AssetBank> bankResult = await assetBankProvisioningService.ProvisionForUnitAsync(context, force, cancelToken);

            if (bankResult.HasSucceeded == false || bankResult.SuccessValue is null)
            {
                const string error = "The force's cargo could not be made ready, so nothing was taken.";
                logger.LogWarning("Could not provision a cargo bank for Unit Instance Id {UnitInstanceId}: {Errors}",
                    force.Id, string.Join("; ", bankResult.Errors));

                return ServiceResult<TakeResourcesResult>.Fail(bankResult.Errors.Prepend(error));
            }

            ServiceResultBase creditResult = await assetBankCargoService.CreditAsync(context, bankResult.SuccessValue, credits, cancelToken);

            if (creditResult.HasSucceeded == false)
            {
                const string error = "The resource could not be loaded, so nothing was taken.";
                logger.LogWarning("Could not credit cargo for Unit Instance Id {UnitInstanceId}: {Errors}",
                    force.Id, string.Join("; ", creditResult.Errors));

                return ServiceResult<TakeResourcesResult>.Fail(creditResult.Errors.Prepend(error));
            }

            foreach (CargoCredit credit in credits)
            {
                gainedByAssetId[credit.AssetId] = gainedByAssetId.GetValueOrDefault(credit.AssetId) + credit.RawQuantity;
            }

            // Loading up is the Force's whole turn, the way a split is.
            force.CampaignMovementRemaining = 0;
            totalTakenRaw += share.UnitsTakenRaw;
        }

        // Floored at 0 rather than trusted to land there, exactly as FoodConsumptionService does: the plan was made against a quantity read a moment ago.
        instance.Quantity = Math.Max(0, instance.Quantity.Value - totalTakenRaw);

        await context.SaveChangesAsync(cancelToken);
        await transaction.CommitAsync(cancelToken);

        logger.LogInformation(
            "Player Id {PlayerId} took {UnitsTaken} of Map Resource Instance Id {MapResourceInstanceId} on Campaign Map Tile Id {TileId} with Force Ids {ForceIds}.",
            request.PlayerId,
            ScaledAmount.FromRaw(totalTakenRaw).ToDecimal(),
            instance.Id,
            instance.CampaignMapId,
            string.Join(", ", forces.Select(force => force.Id)));

        Dictionary<int, string> assetNameById = resource.Yields.ToDictionary(yield => yield.AssetId, yield => yield.AssetDisplayName);

        return ServiceResult<TakeResourcesResult>.Succeed(new TakeResourcesResult
        {
            DisplayName = resource.DisplayName,
            XCoord = forces[0].CampaignMap.XCoord,
            YCoord = forces[0].CampaignMap.YCoord,
            UnitsTaken = (int)ScaledAmount.FromRaw(totalTakenRaw).ToWholeRoundedDown(),
            IsExhausted = instance.Quantity == 0,
            Gained =
            [
                .. gainedByAssetId
                    .OrderBy(entry => entry.Key)
                    .Select(entry => new TakeResourcesAssetLine
                    {
                        AssetId = entry.Key,
                        AssetDisplayName = assetNameById[entry.Key],
                        Amount = (int)ScaledAmount.FromRaw(entry.Value).ToWholeRoundedDown()
                    })
            ],
            Forces = outcomes
        });
    }

    /// <summary>
    /// Refuses the order unless it is this Player's Turn.
    /// </summary>
    /// <remarks>
    /// The same gate <c>StepForceHandler</c>, <c>SettleForceHandler</c> and <c>SplitForceHandler</c> each apply, and for the same reason: the toolbar does
    /// not check whose Turn it is, so the write path has to. See open question 1 in <c>docs/design/units/force-context-action-toolbar.md</c>.
    /// </remarks>
    private async Task<ServiceResult<TakeResourcesResult>?> ValidateItIsThisPlayersTurnAsync(
        TakeResourcesRequest request,
        CancellationToken cancelToken)
    {
        ServiceResult<CurrentCampaignTurn> turnResult = await campaignTurnService.GetCurrentCampaignTurnAsync(request.GameId, cancelToken);

        if (turnResult.HasSucceeded == false || turnResult.SuccessValue is null)
        {
            const string error = "No turn is in progress in this game, so nothing can be taken.";
            logger.LogWarning("No current campaign turn for Game Id {GameId}, so the take-resources order by Player Id {PlayerId} was refused. Errors: {Errors}",
                request.GameId, request.PlayerId, string.Join("; ", turnResult.Errors));

            return ServiceResult<TakeResourcesResult>.Fail(error);
        }

        CurrentCampaignTurn currentTurn = turnResult.SuccessValue;

        if (currentTurn.PlayerId != request.PlayerId)
        {
            string error = $"It is {currentTurn.FactionName}'s turn, so you cannot take resources yet.";
            logger.LogInformation("Player Id {PlayerId} tried to take resources during Player Id {CurrentPlayerId}'s turn in Game Id {GameId}.",
                request.PlayerId, currentTurn.PlayerId, request.GameId);

            return ServiceResult<TakeResourcesResult>.Forbidden(error);
        }

        return null;
    }
}
