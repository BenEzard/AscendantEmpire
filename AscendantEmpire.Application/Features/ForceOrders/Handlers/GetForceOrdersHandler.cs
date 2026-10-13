using AscendantEmpire.Application.Common.Messaging;
using AscendantEmpire.Application.Common.Validation;
using AscendantEmpire.Application.Data;
using AscendantEmpire.BackendContracts.ForceOrders.GetForceOrders;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

using static AscendantEmpire.Application.Common.ServiceResultBase;

namespace AscendantEmpire.Application.Features.ForceOrders.Handlers;

public record GetForceOrdersRequest(
    int GameId,
    int PlayerId,
    int UnitInstanceId
) : IRequest<ServiceResult<IReadOnlyCollection<ForceOrder>>>;

internal class GetForceOrdersRequestValidator : Validator<GetForceOrdersRequest>
{
    public GetForceOrdersRequestValidator()
    {
        RuleFor(x => x.GameId)
            .GreaterThan(0).WithMessage("Game Id is required.");

        RuleFor(x => x.PlayerId)
            .GreaterThan(0).WithMessage("Player Id is required.");

        RuleFor(x => x.UnitInstanceId)
            .GreaterThan(0).WithMessage("Unit Instance Id is required.");
    }
}

internal class GetForceOrdersHandler(
    ILogger<GetForceOrdersHandler> logger,
    IDbContextFactory<AscendantEmpireContext> dbFactory
) : IRequestHandler<GetForceOrdersRequest, ServiceResult<IReadOnlyCollection<ForceOrder>>>
{
    public async Task<ServiceResult<IReadOnlyCollection<ForceOrder>>> Handle(GetForceOrdersRequest request, CancellationToken cancelToken)
    {
        var validator = new GetForceOrdersRequestValidator();
        var validationResult = validator.Validate(request);

        if (!validationResult.IsValid)
        {
            IEnumerable<string> errors = validationResult.Errors.Select(e => e.ErrorMessage);
            string error = $"Validation failed for {nameof(GetForceOrdersRequest)}.";
            logger.LogWarning("{Error} Errors: {Errors}", error, string.Join("; ", errors));
            return ServiceResult<IReadOnlyCollection<ForceOrder>>.Fail(errors.Prepend(error));
        }

        await using AscendantEmpireContext context = await dbFactory.CreateDbContextAsync(cancelToken);

        bool unitExistsForPlayer = await context.UnitInstances.AnyAsync(t =>
                t.Id == request.UnitInstanceId 
                && t.GameId == request.GameId 
                && t.PlayerId == request.PlayerId,
                cancelToken);

        if (!unitExistsForPlayer)
        {
            string error = $"Unit Instance Id {request.UnitInstanceId} was not found for Player Id {request.PlayerId} in Game Id {request.GameId}.";
            logger.LogWarning("Unit Instance Id {UnitInstanceId} was not found for Player Id {PlayerId} in Game Id {GameId}.",
                request.UnitInstanceId,
                request.PlayerId,
                request.GameId);

            return ServiceResult<IReadOnlyCollection<ForceOrder>>.Fail(error);
        }

        List<ForceOrder> orders = await context.ForceOrders
            .AsNoTracking()
            .Where(o => o.GameId == request.GameId)
            .Where(o => o.UnitInstanceId == request.UnitInstanceId)
            .OrderByDescending(o => o.CreatedTurnNumber)
            .ThenByDescending(o => o.Id)
            .Select(o => new ForceOrder
            {
                Id = o.Id,
                GameId = o.GameId,
                UnitInstanceId = o.UnitInstanceId,
                CommandId = o.CommandId,
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
                DefendTargetUnitInstanceId = o.DefendTargetUnitInstanceId,
                DefendTargetStructureInstanceId = o.DefendTargetStructureInstanceId,
                CreatedTurnNumber = o.CreatedTurnNumber,
                LastProcessedTurnNumber = o.LastProcessedTurnNumber,
                CompletedTurnNumber = o.CompletedTurnNumber,
                FailureReason = o.FailureReason,
                CancellationReason = o.CancellationReason,

                MovementSteps = o.MovementSteps
                    .OrderBy(ms => ms.StepIndex)
                    .Select(ms => new ForceOrderMovementStep
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
                    .Select(p => new ForceOrderParticipant
                    {
                        UnitInstanceId = p.UnitInstanceId,
                        Role = p.Role
                    })
                    .ToList()
            })
            .ToListAsync(cancelToken);

        logger.LogInformation(
            "Retrieved {Count} force orders for Unit Instance Id {UnitInstanceId}, Player Id {PlayerId}, Game Id {GameId}.",
            orders.Count,
            request.UnitInstanceId,
            request.PlayerId,
            request.GameId);

        return ServiceResult<IReadOnlyCollection<ForceOrder>>.Succeed(orders);
    }
}