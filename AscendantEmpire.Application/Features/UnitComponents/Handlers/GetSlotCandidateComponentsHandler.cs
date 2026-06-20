using AscendantEmpire.Application.Common;
using AscendantEmpire.Application.Data;
using AscendantEmpire.Application.Features.UnitComponents.Services;
using AscendantEmpire.BackendContracts.UnitDesigns.GetSlotCandidateComponents;
using AscendantEmpire.Domain.Entities.UnitDesigns;
using AscendantEmpire.Domain.Enums.UnitDesigns;

using MediatR;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

using static AscendantEmpire.Application.Common.ServiceResultBase;

namespace AscendantEmpire.Application.Features.UnitComponents.Handlers;

/// <summary>
/// Returns the components a player has unlocked that satisfy a specific body slot's acceptance rules —
/// the feed for the component-selection window. Game and player are supplied from the caller's token.
/// </summary>
public record GetSlotCandidateComponentsRequest(int GameId, int PlayerId, int BodyComponentId, int SlotId)
    : IRequest<ServiceResult<SlotCandidateComponentsResult>>;

internal class GetSlotCandidateComponentsHandler(
    ILogger<GetSlotCandidateComponentsHandler> logger,
    IDbContextFactory<AscendantEmpireContext> dbFactory
) : IRequestHandler<GetSlotCandidateComponentsRequest, ServiceResult<SlotCandidateComponentsResult>>
{
    public async Task<ServiceResult<SlotCandidateComponentsResult>> Handle(GetSlotCandidateComponentsRequest request, CancellationToken cancelToken)
    {
        try
        {
            if (request.GameId <= 0 || request.PlayerId <= 0 || request.BodyComponentId <= 0 || request.SlotId <= 0)
            {
                return ServiceResult<SlotCandidateComponentsResult>.Fail("Game, player, body and slot are required.");
            }

            await using AscendantEmpireContext context = await dbFactory.CreateDbContextAsync(cancelToken);

            // Load the slot with its rules, and confirm it actually belongs to the named body.
            UnitComponentSlot? slot = await context.UnitComponentSlots
                .AsNoTracking()
                .Include(s => s.Rules)
                .FirstOrDefaultAsync(s => s.Id == request.SlotId && s.UnitComponentBodyId == request.BodyComponentId, cancelToken);

            if (slot is null)
            {
                return ServiceResult<SlotCandidateComponentsResult>.NotFound(
                    $"Slot {request.SlotId} was not found on body component {request.BodyComponentId}.");
            }

            IQueryable<int> unlockedComponentIds = context.PlayerUnlockedComponents
                .Where(u => u.PlayerId == request.PlayerId && u.ObsoleteOnTurn == null)
                .Select(u => u.UnitComponentId);

            // Pull the unlocked non-body components (with the stats the selection window shows). The
            // slot-rule filter is applied in memory because it is shared, non-trivial domain logic.
            List<DesignComponent> unlocked = await context.UnitComponents
                .AsNoTracking()
                .Where(c => c.ComponentType != ComponentType.Body && unlockedComponentIds.Contains(c.Id))
                .Select(DesignComponentProjections.ToDesignComponent)
                .ToListAsync(cancelToken);

            List<DesignComponent> accepted = unlocked
                .Where(component => SlotRuleEvaluator.Accepts(component.ComponentType, component.TagCodes, slot.Rules))
                .ToList();

            return ServiceResult<SlotCandidateComponentsResult>.Succeed(new SlotCandidateComponentsResult { Components = accepted });
        }
        catch (Exception ex)
        {
            const string error = "An exception occurred while processing " + nameof(GetSlotCandidateComponentsHandler) + ".";
            logger.LogError(ex, error);
            return ServiceResult<SlotCandidateComponentsResult>.InternalServerError(ex, error);
        }
    }
}
