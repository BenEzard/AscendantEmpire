using AscendantEmpire.Application.Common;
using AscendantEmpire.Application.Data;
using AscendantEmpire.Application.Features.Game.Handlers;
using AscendantEmpire.Application.Features.Turns.Handlers;
using AscendantEmpire.Application.Features.Turns.Services;
using AscendantEmpire.Application.Helpers;
using AscendantEmpire.BackendContracts.Games.GetOpenGames;
using AscendantEmpire.BackendContracts.TroopOrders.MoveTroops;
using AscendantEmpire.BackendContracts.Turns.GetCurrentCampaignTurn;
using AscendantEmpire.BackendContracts.Turns.GetPlayerTurn;
using AscendantEmpire.Domain.Entities.Units;

using FluentValidation;

using MediatR;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

using static AscendantEmpire.Application.Common.ServiceResultBase;

namespace AscendantEmpire.Application.Features.TroopOrders.Handlers;

/// <summary>
/// A request to move a Troop to a new location on the Battle/Campaign Map.
/// </summary>
/// <param name="BattleMapTileId">If this is null, then the movement order is for the Campaign Map.</param>
public record MoveTroopRequest(
    int GameId,
    int PlayerId,
    int TroopInstanceId,
    //int? BattleMapTileId,
    int XCoord,
    int YCoord
) : IRequest<ServiceResult<MoveTroopResult>>;

internal class MoveTroopRequestValidator : AbstractValidator<MoveTroopRequest>
{
    public MoveTroopRequestValidator()
    {
        RuleFor(x => x.GameId)
            .GreaterThan(0).WithMessage("Game Id is required.");

        RuleFor(x => x.PlayerId)
            .GreaterThan(0).WithMessage("Player Id is required.");

        RuleFor(x => x.TroopInstanceId)
            .GreaterThan(0).WithMessage("Troop Instance Id is required.");

        RuleFor(x => x.XCoord)
            .GreaterThanOrEqualTo(0).WithMessage("X Coordinate is required.");

        RuleFor(x => x.YCoord)
            .GreaterThanOrEqualTo(0).WithMessage("Y Coordinate is required.");
    }
}

internal class MoveTroopHandler(
    ILogger<MoveTroopHandler> logger,
    IDbContextFactory<AscendantEmpireContext> dbFactory,
    IMediator mediator,
    ICampaignTurnService campaignTurnService//,
    //IPathFinder pathFinder
) : IRequestHandler<MoveTroopRequest, ServiceResult<MoveTroopResult>>
{
    public async Task<ServiceResult<MoveTroopResult>> Handle(MoveTroopRequest request, CancellationToken cancelToken)
    {
        try
        {
            var validator = new MoveTroopRequestValidator();
            var validationResult = validator.Validate(request);
            if (!validationResult.IsValid)
            {
                IEnumerable<string> errors = validationResult.Errors.Select(e => e.ErrorMessage);
                string error = $"Validation failed for {nameof(MoveTroopRequest)}.";
                logger.LogWarning("{Error} Errors: {Errors}", error, string.Join("; ", errors));
                return ServiceResult<MoveTroopResult>.Fail(errors.Prepend(error));
            }

            await using AscendantEmpireContext context = await dbFactory.CreateDbContextAsync(cancelToken);

            TroopInstance? troop = await context.TroopInstances
                .Include(ti => ti.UnitTemplate.Compiled)
                .FirstOrDefaultAsync(ti => ti.Id == request.TroopInstanceId && ti.GameId == request.GameId, cancelToken);

            ServiceResult<IReadOnlyCollection<OpenGame>> gameInfoResult = await mediator.Send(new GetOpenGamesRequest(request.GameId, request.PlayerId), cancelToken);

            if (gameInfoResult.HasSucceeded == false || gameInfoResult.SuccessValue is null || !gameInfoResult.SuccessValue.Any())
            {
                const string error = "Error getting game info while trying to move Troops";
                logger.LogWarning(error);
                return ServiceResult<MoveTroopResult>.Fail(gameInfoResult.Errors.Prepend(error));
            }

            OpenGame gameInfo = gameInfoResult.SuccessValue!.Single();

            int targetCampaignMapTileId = MapMethods.GetDbId(
                gameInfo.Players.Single().CampaignMapOffsetId,
                gameInfo.MapWidth,
                x: request.XCoord,
                y: request.YCoord);

            // TODO Check who's turn it is
            ServiceResult<CurrentCampaignTurn> turnResult = await campaignTurnService.GetCurrentCampaignTurnAsync(request.GameId, cancelToken);

            if (turnResult.HasSucceeded == false || turnResult.SuccessValue is null)
            {
                const string error = "Error getting turn info while trying to move Troops";
                logger.LogWarning(error);
                return ServiceResult<MoveTroopResult>.Fail(turnResult.Errors.Prepend(error));
            }

            //List<RosterEntry> rosterEntriesForPlayer = [.. turnResult.SuccessValue.Where(re => re.CurrentTurnPlayerId == request.PlayerId)];


            //ServiceResultBase secondaryValidationResult = ValidateObjects(request, troop, targetCampaignMapTileId, rosterEntriesForPlayer);
            //if (!secondaryValidationResult.HasSucceeded || troop is null)
            //{
            //    logger.LogWarning("Secondary validation failed for {Request}. Errors: {Errors}", request, string.Join("; ", secondaryValidationResult.Errors));
            //    return ServiceResult<MoveTroopResult>.Fail(secondaryValidationResult.Errors);
            //}

            // TODO: Get active orders, and if there are any, cancel them.





            /*            
             *            bool hasActiveOrder = await context.TroopOrders
                            .AnyAsync(to =>
                                to.TroopInstanceId == request.TroopInstanceId 
                                && (to.OrderStatus == TroopOrderStatus.Pending 
                                    || to.OrderStatus == TroopOrderStatus.InProgress 
                                    || to.OrderStatus == TroopOrderStatus.Paused),
                                cancelToken);

                        if (hasActiveOrder)
                        {
                            string error = $"Troop Instance Id {request.TroopInstanceId} already has an active order.";
                            logger.LogWarning(error);
                            return ServiceResult<MoveTroopResult>.Fail(error);
                        }*/

            /*CampaignPathResult? pathResult = await pathFinder.FindPathAsync(
                context,
                request.GameId,
                request.TroopInstanceId,
                troop.CampaignMapTileId,
                targetCampaignMapTileId,
                cancelToken);

            if (pathResult is null || !pathResult.Steps.Any())
            {
                string error = $"No valid path could be found for Troop Instance Id {request.TroopInstanceId} to Campaign Map Tile Id {request.TargetCampaignMapTileId}.";
                logger.LogWarning(error);
                return ServiceResult<MoveTroopResult>.Fail(error);
            }

            int currentTurnNumber = await context.TurnRosters
                .Where(x => x.GameId == request.GameId)
                .Select(x => (int?)x.CurrentTurnNumber)
                .FirstOrDefaultAsync(cancelToken)
                ?? 0;

            var troopOrder = new TroopOrder
            {
                GameId = request.GameId,
                TroopInstanceId = request.TroopInstanceId,
                OrderType = TroopOrderType.Move,
                OrderStatus = TroopOrderStatus.Pending,
                TargetCampaignMapTileId = request.TargetCampaignMapTileId,
                CurrentMovementStepIndex = 0,
                WorkRequired = null,
                WorkCompleted = 0,
                CreatedTurnNumber = currentTurnNumber,
                LastProcessedTurnNumber = null,
                CompletedTurnNumber = null,
                FailureReason = null,
                CancellationReason = null
            };

            context.TroopOrders.Add(troopOrder);
            await context.SaveChangesAsync(cancelToken);

            List<TroopOrderMovementStep> movementSteps = pathResult.Steps
                .Select((step, index) => new TroopOrderMovementStep
                {
                    TroopOrderId = troopOrder.Id,
                    StepIndex = index,
                    CampaignMapTileId = step.CampaignMapTileId,
                    XCoord = step.XCoord,
                    YCoord = step.YCoord,
                    MovementCost = step.MovementCost,
                    IsConsumed = false,
                    ConsumedTurnNumber = null
                })
                .ToList();

            context.TroopOrderMovementSteps.AddRange(movementSteps);
            await context.SaveChangesAsync(cancelToken);

            logger.LogInformation(
                "Created move order {TroopOrderId} for Troop Instance Id {TroopInstanceId} in Game Id {GameId} to Campaign Map Tile Id {TargetCampaignMapTileId} with {PathStepCount} path steps.",
                troopOrder.Id,
                request.TroopInstanceId,
                request.GameId,
                request.TargetCampaignMapTileId,
                movementSteps.Count);

            var result = new MoveTroopResult
            {
                TroopOrderId = troopOrder.Id,
                TroopInstanceId = troop.Id,
                TargetCampaignMapTileId = request.TargetCampaignMapTileId,
                PathStepCount = movementSteps.Count,
                EstimatedTurns = pathResult.EstimatedTurns
            };*/

            return ServiceResult<MoveTroopResult>.Succeed(null);
        }
        catch (Exception ex)
        {
            const string error = "An exception occurred while trying to create a move order for Troop.";
            
            logger.LogError(ex, error + " GameId: {request.GameId}, TroopInstanceId: {TroopInstanceId}, X: {X}, Y: {Y}.",
                request.GameId,
                request.TroopInstanceId,
                request.XCoord,
                request.YCoord);

            return ServiceResult<MoveTroopResult>.Fail(error);
        }
    }

    /// <summary>
    /// Do secondary validation once we have loaded the dependencies.
    /// </summary>
    /// <remarks>Checks the following things:
    /// <list type="bullet">
    /// <item>Troop instance exists</item>
    /// <item>Troop instance belongs to the player</item>
    /// <item>Troop instance has remaining healthy or injured troops</item>
    /// <item>Troop instance is not currently in battle</item>
    /// <item>Troop instance is not already on the target campaign map tile</item>
    /// <item>Troop instance has remaining movement points</item>
    /// <item>It is currently the Player's turn</item>
    /// </list>
    /// </remarks>
    private ServiceResultBase ValidateObjects(MoveTroopRequest request, TroopInstance? troop, int targetCampaignMapTileId, IReadOnlyCollection<RosterEntry> rosterEntries)
    {
        if (troop is null)
        {
            string error = $"Troop Instance Id {request.TroopInstanceId} was not found for Game Id {request.GameId}.";
            logger.LogWarning("Troop Instance Id {TroopInstanceId} was not found for Game Id {GameId}.", request.TroopInstanceId, request.GameId);
            return Fail(error);
        }

        if (troop.PlayerId != request.PlayerId)
        {
            string error = $"Troop Instance Id {request.TroopInstanceId} does not belong to Player Id {request.PlayerId}.";
            logger.LogWarning("Troop Instance Id {TroopInstanceId} does not belong to Player Id {PlayerId}.", request.TroopInstanceId, request.PlayerId);
            return Fail(error);
        }

        if (troop.HealthyCount + troop.InjuredCount <= 0)
        {
            // TODO: If this happens, we need to auto-delete the troops and any orders.
            string error = $"Troop Instance Id {request.TroopInstanceId} has no remaining troops and cannot move.";
            logger.LogWarning("Troop Instance Id {TroopInstanceId} has no remaining troops and cannot move.", request.TroopInstanceId);
            return Fail(error);
        }

        if (troop.BattleMapTileId is not null)
        {
            string error = $"Troop Instance Id {request.TroopInstanceId} is currently in battle and cannot receive a campaign move order.";
            logger.LogWarning("Troop Instance Id {TroopInstanceId} is currently in battle and cannot receive a campaign move order.", request.TroopInstanceId);
            return Fail(error);
        }

        if (troop.CampaignMapTileId == targetCampaignMapTileId)
        {
            string error = $"Troop Instance Id {request.TroopInstanceId} is already on Campaign Map Tile Id {targetCampaignMapTileId}.";
            logger.LogWarning("Troop Instance Id {TroopInstanceId} is already on Campaign Map Tile Id {TargetCampaignMapTileId}.", request.TroopInstanceId, targetCampaignMapTileId);
            return Fail(error);
        }

        if (troop.MovementPointsRemaining <= 0)
        {
            string error = $"Troop Instance Id {request.TroopInstanceId} has no remaining movement points and cannot move.";
            logger.LogWarning("Troop Instance Id {TroopInstanceId} has no remaining movement points and cannot move.", request.TroopInstanceId);
            return Fail(error);
        }

        if (rosterEntries.Any())
        {
            string error = $"It is not currently Player Id {request.PlayerId}'s turn and they cannot move troops.";
            logger.LogWarning("It is not currently Player Id {PlayerId}'s turn and they cannot move troops.", request.PlayerId);
            return Fail(error);
        }

        return SuccessNoContent();
    }
}