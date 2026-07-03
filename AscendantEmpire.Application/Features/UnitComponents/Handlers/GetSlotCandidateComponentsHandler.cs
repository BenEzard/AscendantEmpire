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
/// Returns the components a player has unlocked that satisfy a specific slot's acceptance rules — the
/// feed for the component-selection window. The slot may belong to any component (a body or a fitted
/// component that exposes its own slots). Game and player are supplied from the caller's token.
/// </summary>
public record GetSlotCandidateComponentsRequest(int GameId, int PlayerId, int OwnerComponentId, int SlotId)
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
            if (request.GameId <= 0 || request.PlayerId <= 0 || request.OwnerComponentId <= 0 || request.SlotId <= 0)
            {
                return ServiceResult<SlotCandidateComponentsResult>.Fail("Game, player, owner component and slot are required.");
            }

            await using AscendantEmpireContext context = await dbFactory.CreateDbContextAsync(cancelToken);

            // Load the slot with its rules, and confirm it actually belongs to the named owner component
            // (which may be a body or any fitted component that exposes slots).
            UnitComponentSlot? slot = await context.UnitComponentSlots
                .AsNoTracking()
                .Include(s => s.Rules)
                .FirstOrDefaultAsync(s => s.Id == request.SlotId && s.UnitComponentId == request.OwnerComponentId, cancelToken);

            if (slot is null)
            {
                return ServiceResult<SlotCandidateComponentsResult>.NotFound(
                    $"Slot {request.SlotId} was not found on component {request.OwnerComponentId}.");
            }

            // The slots the player has unlocked, used both to gate this target slot and to overlay the
            // per-player lock state onto any slots the candidate components themselves expose.
            HashSet<int> unlockedSlotIds = await context.PlayerUnlockedSlots
                .Where(u => u.PlayerId == request.PlayerId && u.ObsoleteOnTurn == null)
                .Select(u => u.UnitComponentSlotId)
                .ToHashSetAsync(cancelToken);

            // A design-locked slot the player hasn't unlocked can't be filled, so it offers no candidates.
            bool slotLockedForPlayer = slot.IsLocked && !unlockedSlotIds.Contains(slot.Id);

            if (slotLockedForPlayer)
            {
                return ServiceResult<SlotCandidateComponentsResult>.Succeed(new SlotCandidateComponentsResult { Components = [] });
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
                .Select(component => component with
                {
                    // A candidate may itself expose slots, so overlay the player's unlocks on those too.
                    Slots = DesignComponentProjections.WithPlayerLockState(component.Slots, unlockedSlotIds),
                })
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
