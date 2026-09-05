using AscendantEmpire.Application.Common;
using AscendantEmpire.Application.Common.Validation;
using AscendantEmpire.Application.Data;
using AscendantEmpire.Application.Features.Game.Handlers;
using AscendantEmpire.Application.Features.Turns.Handlers;
using AscendantEmpire.Application.Features.Turns.Services;
using AscendantEmpire.BackendContracts.Games.GetOpenGames;
using AscendantEmpire.BackendContracts.ForceOrders.MoveForces;
using AscendantEmpire.BackendContracts.Turns.GetCurrentCampaignTurn;
using AscendantEmpire.BackendContracts.Turns.GetPlayerTurn;
using AscendantEmpire.Domain.Entities.Games;
using AscendantEmpire.Domain.Entities.Units;

using MediatR;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

using static AscendantEmpire.Application.Common.ServiceResultBase;

namespace AscendantEmpire.Application.Features.ForceOrders.Handlers;

/// <summary>
/// A request to move a Force to a new location on the Battle/Campaign Map.
/// </summary>
/// <param name="BattleMapTileId">If this is null, then the movement order is for the Campaign Map.</param>
public record MoveForceRequest(
    int GameId,
    int PlayerId,
    int UnitInstanceId,
    //int? BattleMapTileId,
    int XCoord,
    int YCoord
) : IRequest<ServiceResult<MoveForceResult>>;

internal class MoveForceRequestValidator : Validator<MoveForceRequest>
{
    public MoveForceRequestValidator()
    {
        RuleFor(x => x.GameId)
            .GreaterThan(0).WithMessage("Game Id is required.");

        RuleFor(x => x.PlayerId)
            .GreaterThan(0).WithMessage("Player Id is required.");

        RuleFor(x => x.UnitInstanceId)
            .GreaterThan(0).WithMessage("Unit Instance Id is required.");

        RuleFor(x => x.XCoord)
            .GreaterThanOrEqualTo(0).WithMessage("X Coordinate is required.");

        RuleFor(x => x.YCoord)
            .GreaterThanOrEqualTo(0).WithMessage("Y Coordinate is required.");
    }
}

internal class MoveForceHandler(
    ILogger<MoveForceHandler> logger,
    IDbContextFactory<AscendantEmpireContext> dbFactory,
    IMediator mediator,
    ICampaignTurnService campaignTurnService//,
    //IPathFinder pathFinder
) : IRequestHandler<MoveForceRequest, ServiceResult<MoveForceResult>>
{
    public async Task<ServiceResult<MoveForceResult>> Handle(MoveForceRequest request, CancellationToken cancelToken)
    {
        try
        {
            var validator = new MoveForceRequestValidator();
            var validationResult = validator.Validate(request);
            if (!validationResult.IsValid)
            {
                IEnumerable<string> errors = validationResult.Errors.Select(e => e.ErrorMessage);
                string error = $"Validation failed for {nameof(MoveForceRequest)}.";
                logger.LogWarning("{Error} Errors: {Errors}", error, string.Join("; ", errors));
                return ServiceResult<MoveForceResult>.Fail(errors.Prepend(error));
            }

            await using AscendantEmpireContext context = await dbFactory.CreateDbContextAsync(cancelToken);

            UnitInstance? force = await context.UnitInstances
                .Include(ti => ti.UnitTemplate.Compiled)
                .FirstOrDefaultAsync(ti => ti.Id == request.UnitInstanceId && ti.GameId == request.GameId, cancelToken);

            ServiceResult<IReadOnlyCollection<OpenGame>> gameInfoResult = await mediator.Send(new GetOpenGamesRequest(request.GameId, request.PlayerId), cancelToken);

            if (gameInfoResult.HasSucceeded == false || gameInfoResult.SuccessValue is null || !gameInfoResult.SuccessValue.Any())
            {
                const string error = "Error getting game info while trying to move Forces";
                logger.LogWarning(error);
                return ServiceResult<MoveForceResult>.Fail(gameInfoResult.Errors.Prepend(error));
            }

            OpenGame gameInfo = gameInfoResult.SuccessValue!.Single();

            // Resolve the destination to this game's actual tile row. This used to derive an id from the
            // coordinates using the *player's* offset, which addresses the per-player map rather than the
            // world map the Force is stored against, so it named a row in the wrong table's id space
            // entirely. Looking the tile up also rejects a coordinate that is off the map, which nothing
            // else checks: the validator only requires the coordinates to be non-negative.
            CampaignMap? targetTile = await context.CampaignMaps
                .AsNoTracking()
                .FirstOrDefaultAsync(cm => cm.GameId == request.GameId
                        && cm.XCoord == request.XCoord
                        && cm.YCoord == request.YCoord,
                    cancelToken);

            if (targetTile is null)
            {
                string error = $"Coordinate ({request.XCoord}, {request.YCoord}) is not on the campaign map for Game Id {request.GameId}.";
                logger.LogWarning("{Error}", error);
                return ServiceResult<MoveForceResult>.NotFound(error);
            }

            int targetCampaignMapTileId = targetTile.CampaignMapId;

            // TODO Check who's turn it is
            ServiceResult<CurrentCampaignTurn> turnResult = await campaignTurnService.GetCurrentCampaignTurnAsync(request.GameId, cancelToken);

            if (turnResult.HasSucceeded == false || turnResult.SuccessValue is null)
            {
                const string error = "Error getting turn info while trying to move Forces";
                logger.LogWarning(error);
                return ServiceResult<MoveForceResult>.Fail(turnResult.Errors.Prepend(error));
            }

            //List<RosterEntry> rosterEntriesForPlayer = [.. turnResult.SuccessValue.Where(re => re.CurrentTurnPlayerId == request.PlayerId)];


            //ServiceResultBase secondaryValidationResult = ValidateObjects(request, Force, targetCampaignMapTileId, rosterEntriesForPlayer);
            //if (!secondaryValidationResult.HasSucceeded || Force is null)
            //{
            //    logger.LogWarning("Secondary validation failed for {Request}. Errors: {Errors}", request, string.Join("; ", secondaryValidationResult.Errors));
            //    return ServiceResult<MoveForceResult>.Fail(secondaryValidationResult.Errors);
            //}

            // TODO: Get active orders, and if there are any, cancel them.





            /*            
             *            bool hasActiveOrder = await context.ForceOrders
                            .AnyAsync(to =>
                                to.UnitInstanceId == request.UnitInstanceId 
                                && (to.OrderStatus == ForceOrderStatus.Pending 
                                    || to.OrderStatus == ForceOrderStatus.InProgress 
                                    || to.OrderStatus == ForceOrderStatus.Paused),
                                cancelToken);

                        if (hasActiveOrder)
                        {
                            string error = $"Unit Instance Id {request.UnitInstanceId} already has an active order.";
                            logger.LogWarning(error);
                            return ServiceResult<MoveForceResult>.Fail(error);
                        }*/

            /*CampaignPathResult? pathResult = await pathFinder.FindPathAsync(
                context,
                request.GameId,
                request.UnitInstanceId,
                force.CampaignMapTileId,
                targetCampaignMapTileId,
                cancelToken);

            if (pathResult is null || !pathResult.Steps.Any())
            {
                string error = $"No valid path could be found for Unit Instance Id {request.UnitInstanceId} to Campaign Map Tile Id {request.TargetCampaignMapTileId}.";
                logger.LogWarning(error);
                return ServiceResult<MoveForceResult>.Fail(error);
            }

            int currentTurnNumber = await context.TurnRosters
                .Where(x => x.GameId == request.GameId)
                .Select(x => (int?)x.CurrentTurnNumber)
                .FirstOrDefaultAsync(cancelToken)
                ?? 0;

            var forceOrder = new ForceOrder
            {
                GameId = request.GameId,
                UnitInstanceId = request.UnitInstanceId,
                OrderType = ForceOrderType.Move,
                OrderStatus = ForceOrderStatus.Pending,
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

            context.ForceOrders.Add(forceOrder);
            await context.SaveChangesAsync(cancelToken);

            List<ForceOrderMovementStep> movementSteps = pathResult.Steps
                .Select((step, index) => new ForceOrderMovementStep
                {
                    ForceOrderId = forceOrder.Id,
                    StepIndex = index,
                    CampaignMapTileId = step.CampaignMapTileId,
                    XCoord = step.XCoord,
                    YCoord = step.YCoord,
                    MovementCost = step.MovementCost,
                    IsConsumed = false,
                    ConsumedTurnNumber = null
                })
                .ToList();

            context.ForceOrderMovementSteps.AddRange(movementSteps);
            await context.SaveChangesAsync(cancelToken);

            logger.LogInformation(
                "Created move order {ForceOrderId} for Unit Instance Id {UnitInstanceId} in Game Id {GameId} to Campaign Map Tile Id {TargetCampaignMapTileId} with {PathStepCount} path steps.",
                forceOrder.Id,
                request.UnitInstanceId,
                request.GameId,
                request.TargetCampaignMapTileId,
                movementSteps.Count);

            var result = new MoveForceResult
            {
                ForceOrderId = forceOrder.Id,
                UnitInstanceId = force.Id,
                TargetCampaignMapTileId = request.TargetCampaignMapTileId,
                PathStepCount = movementSteps.Count,
                EstimatedTurns = pathResult.EstimatedTurns
            };*/

            return ServiceResult<MoveForceResult>.Succeed(null);
        }
        catch (Exception ex)
        {
            const string error = "An exception occurred while trying to create a move order for Force.";
            
            logger.LogError(ex, error + " GameId: {request.GameId}, UnitInstanceId: {UnitInstanceId}, X: {X}, Y: {Y}.",
                request.GameId,
                request.UnitInstanceId,
                request.XCoord,
                request.YCoord);

            return ServiceResult<MoveForceResult>.Fail(error);
        }
    }

    /// <summary>
    /// Do secondary validation once we have loaded the dependencies.
    /// </summary>
    /// <remarks>Checks the following things:
    /// <list type="bullet">
    /// <item>Force instance exists</item>
    /// <item>Force instance belongs to the player</item>
    /// <item>Force instance has remaining healthy or injured Forces</item>
    /// <item>Force instance is not currently in battle</item>
    /// <item>Force instance is not already on the target campaign map tile</item>
    /// <item>Force instance has remaining movement points</item>
    /// <item>It is currently the Player's turn</item>
    /// </list>
    /// </remarks>
    private ServiceResultBase ValidateObjects(MoveForceRequest request, UnitInstance? force, int targetCampaignMapTileId, IReadOnlyCollection<RosterEntry> rosterEntries)
    {
        if (force is null)
        {
            string error = $"Unit Instance Id {request.UnitInstanceId} was not found for Game Id {request.GameId}.";
            logger.LogWarning("Unit Instance Id {UnitInstanceId} was not found for Game Id {GameId}.", request.UnitInstanceId, request.GameId);
            return Fail(error);
        }

        if (force.PlayerId != request.PlayerId)
        {
            string error = $"Unit Instance Id {request.UnitInstanceId} does not belong to Player Id {request.PlayerId}.";
            logger.LogWarning("Unit Instance Id {UnitInstanceId} does not belong to Player Id {PlayerId}.", request.UnitInstanceId, request.PlayerId);
            return Fail(error);
        }

        if (force.HealthyCount + force.InjuredCount <= 0)
        {
            // TODO: If this happens, we need to auto-delete the Forces and any orders.
            string error = $"Unit Instance Id {request.UnitInstanceId} has no remaining forces and cannot move.";
            logger.LogWarning("Unit Instance Id {UnitInstanceId} has no remaining forces and cannot move.", request.UnitInstanceId);
            return Fail(error);
        }

        if (force.BattleMapTileId is not null)
        {
            string error = $"Unit Instance Id {request.UnitInstanceId} is currently in battle and cannot receive a campaign move order.";
            logger.LogWarning("Unit Instance Id {UnitInstanceId} is currently in battle and cannot receive a campaign move order.", request.UnitInstanceId);
            return Fail(error);
        }

        if (force.CampaignMapTileId == targetCampaignMapTileId)
        {
            string error = $"Unit Instance Id {request.UnitInstanceId} is already on Campaign Map Tile Id {targetCampaignMapTileId}.";
            logger.LogWarning("Unit Instance Id {UnitInstanceId} is already on Campaign Map Tile Id {TargetCampaignMapTileId}.", request.UnitInstanceId, targetCampaignMapTileId);
            return Fail(error);
        }

        // Remaining MP is raw fixed-point (Scale 1,000), but "none left" is zero at any scale, so this comparison is unaffected by the representation.
        if (force.CampaignMovementRemaining <= 0)
        {
            string error = $"Unit Instance Id {request.UnitInstanceId} has no remaining movement points and cannot move.";
            logger.LogWarning("Unit Instance Id {UnitInstanceId} has no remaining movement points and cannot move.", request.UnitInstanceId);
            return Fail(error);
        }

        if (rosterEntries.Any())
        {
            string error = $"It is not currently Player Id {request.PlayerId}'s turn and they cannot move forces.";
            logger.LogWarning("It is not currently Player Id {PlayerId}'s turn and they cannot move forces.", request.PlayerId);
            return Fail(error);
        }

        return SuccessNoContent();
    }
}