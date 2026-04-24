using AscendantEmpire.Application.Data;
using AscendantEmpire.BackendContracts.TroopOrders.GetTroopOrders;

using FluentValidation;

using MediatR;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

using static AscendantEmpire.Application.Common.ServiceResultBase;

namespace AscendantEmpire.Application.Features.TroopOrders.Handlers;

public record GetTroopOrdersRequest(
    int GameId,
    int PlayerId,
    int TroopInstanceId
) : IRequest<ServiceResult<IReadOnlyCollection<TroopOrder>>>;

internal class GetTroopOrdersRequestValidator : AbstractValidator<GetTroopOrdersRequest>
{
    public GetTroopOrdersRequestValidator()
    {
        RuleFor(x => x.GameId)
            .GreaterThan(0).WithMessage("Game Id is required.");

        RuleFor(x => x.PlayerId)
            .GreaterThan(0).WithMessage("Player Id is required.");

        RuleFor(x => x.TroopInstanceId)
            .GreaterThan(0).WithMessage("Troop Instance Id is required.");
    }
}

internal class GetTroopOrdersHandler(
    ILogger<GetTroopOrdersHandler> logger,
    IDbContextFactory<AscendantEmpireContext> dbFactory
) : IRequestHandler<GetTroopOrdersRequest, ServiceResult<IReadOnlyCollection<TroopOrder>>>
{
    public async Task<ServiceResult<IReadOnlyCollection<TroopOrder>>> Handle(GetTroopOrdersRequest request, CancellationToken cancelToken)
    {
        try
        {
            var validator = new GetTroopOrdersRequestValidator();
            var validationResult = validator.Validate(request);

            if (!validationResult.IsValid)
            {
                IEnumerable<string> errors = validationResult.Errors.Select(e => e.ErrorMessage);
                string error = $"Validation failed for {nameof(GetTroopOrdersRequest)}.";
                logger.LogWarning("{Error} Errors: {Errors}", error, string.Join("; ", errors));
                return ServiceResult<IReadOnlyCollection<TroopOrder>>.Fail(errors.Prepend(error));
            }

            await using AscendantEmpireContext context = await dbFactory.CreateDbContextAsync(cancelToken);

            bool troopExistsForPlayer = await context.TroopInstances.AnyAsync(t =>
                    t.Id == request.TroopInstanceId 
                    && t.GameId == request.GameId 
                    && t.PlayerId == request.PlayerId,
                    cancelToken);

            if (!troopExistsForPlayer)
            {
                string error = $"Troop Instance Id {request.TroopInstanceId} was not found for Player Id {request.PlayerId} in Game Id {request.GameId}.";
                logger.LogWarning("Troop Instance Id {TroopInstanceId} was not found for Player Id {PlayerId} in Game Id {GameId}.",
                    request.TroopInstanceId,
                    request.PlayerId,
                    request.GameId);

                return ServiceResult<IReadOnlyCollection<TroopOrder>>.Fail(error);
            }

            List<TroopOrder> orders = await context.TroopOrders
                .AsNoTracking()
                .Where(o => o.GameId == request.GameId)
                .Where(o => o.TroopInstanceId == request.TroopInstanceId)
                .OrderByDescending(o => o.CreatedTurnNumber)
                .ThenByDescending(o => o.Id)
                .Select(o => new TroopOrder
                {
                    Id = o.Id,
                    GameId = o.GameId,
                    TroopInstanceId = o.TroopInstanceId,
                    OrderType = o.OrderType,
                    OrderStatus = o.OrderStatus,
                    TargetCampaignMapTileId = o.TargetCampaignMapTileId,
                    StructureInstanceId = o.StructureInstanceId,
                    ResourceDefinitionId = o.ResourceDefinitionId,
                    CurrentMovementStepIndex = o.CurrentMovementStepIndex,
                    WorkRequired = o.WorkRequired,
                    WorkCompleted = o.WorkCompleted,
                    EffectiveFromTurnNumber = o.EffectiveFromTurnNumber,
                    TradeSourceStructureInstanceId = o.TradeSourceStructureInstanceId,
                    TradeTargetStructureInstanceId = o.TradeTargetStructureInstanceId,
                    DefendTargetTroopInstanceId = o.DefendTargetTroopInstanceId,
                    DefendTargetStructureInstanceId = o.DefendTargetStructureInstanceId,
                    CreatedTurnNumber = o.CreatedTurnNumber,
                    LastProcessedTurnNumber = o.LastProcessedTurnNumber,
                    CompletedTurnNumber = o.CompletedTurnNumber,
                    FailureReason = o.FailureReason,
                    CancellationReason = o.CancellationReason,

                    MovementSteps = o.MovementSteps
                        .OrderBy(ms => ms.StepIndex)
                        .Select(ms => new TroopOrderMovementStep
                        {
                            Id = ms.Id,
                            StepIndex = ms.StepIndex,
                            CampaignMapTileId = ms.CampaignMapTileId,
                            MovementCost = ms.MovementCost,
                            IsCompleted = ms.IsCompleted,
                            CompletedTurnNumber = ms.CompletedTurnNumber
                        })
                        .ToList(),

                    Participants = o.Participants
                        .Select(p => new TroopOrderParticipant
                        {
                            TroopInstanceId = p.TroopInstanceId,
                            Role = p.Role
                        })
                        .ToList()
                })
                .ToListAsync(cancelToken);

            logger.LogInformation(
                "Retrieved {Count} troop orders for Troop Instance Id {TroopInstanceId}, Player Id {PlayerId}, Game Id {GameId}.",
                orders.Count,
                request.TroopInstanceId,
                request.PlayerId,
                request.GameId);

            return ServiceResult<IReadOnlyCollection<TroopOrder>>.Succeed(orders);
        }
        catch (Exception ex)
        {
            string error = $"An error occurred while retrieving troop orders for Troop Instance Id {request.TroopInstanceId}, Player Id {request.PlayerId}, Game Id {request.GameId}.";
            logger.LogError(ex, "An error occurred while retrieving troop orders for Troop Instance Id {TroopInstanceId}, Player Id {PlayerId}, Game Id {GameId}.", 
                request.TroopInstanceId, 
                request.PlayerId, 
                request.GameId);

            return ServiceResult<IReadOnlyCollection<TroopOrder>>.Fail(error);
        }
    }
}