using AscendantEmpire.Application.Common;
using AscendantEmpire.Application.Data;
using AscendantEmpire.Domain.Entities.UnitDesigns;

using MediatR;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

using static AscendantEmpire.Application.Common.ServiceResultBase;

namespace AscendantEmpire.Application.Features.UnitComponents.Handlers;

/// <summary>
/// Unlocks a single otherwise-locked component slot for a player by recording a
/// <see cref="PlayerUnlockedSlot"/> row. Invoked from in-game backend logic (e.g. the tech-advance
/// system), not exposed through any controller endpoint — the player never triggers this directly.
/// </summary>
/// <remarks>
/// Idempotent: if the player already has an active unlock for the slot, the command succeeds without
/// writing a second row.
/// </remarks>
public record UnlockSlotForPlayerCommand(int PlayerId, int UnitComponentSlotId, int UnlockedOnTurn)
    : IRequest<ServiceResult<Empty>>;

internal class UnlockSlotForPlayerHandler(
    ILogger<UnlockSlotForPlayerHandler> logger,
    IDbContextFactory<AscendantEmpireContext> dbFactory
) : IRequestHandler<UnlockSlotForPlayerCommand, ServiceResult<Empty>>
{
    public async Task<ServiceResult<Empty>> Handle(UnlockSlotForPlayerCommand request, CancellationToken cancelToken)
    {
        try
        {
            // This is called from trusted game logic, so a lightweight guard is enough.
            if (request.PlayerId <= 0 || request.UnitComponentSlotId <= 0)
            {
                return ServiceResult<Empty>.Fail("Player and slot are required.");
            }

            if (request.UnlockedOnTurn < 0)
            {
                return ServiceResult<Empty>.Fail("UnlockedOnTurn must be zero or greater.");
            }

            await using AscendantEmpireContext context = await dbFactory.CreateDbContextAsync(cancelToken);

            // Idempotent: an active row (not yet obsolete) means the slot is already unlocked for this
            // player, so there is nothing to do.
            bool alreadyUnlocked = await context.PlayerUnlockedSlots
                .AnyAsync(
                    u => u.PlayerId == request.PlayerId
                        && u.UnitComponentSlotId == request.UnitComponentSlotId
                        && u.ObsoleteOnTurn == null,
                    cancelToken);

            if (alreadyUnlocked)
            {
                return ServiceResult<Empty>.SuccessNoContent();
            }

            context.PlayerUnlockedSlots.Add(new PlayerUnlockedSlot
            {
                PlayerId = request.PlayerId,
                UnitComponentSlotId = request.UnitComponentSlotId,
                UnlockedOnTurn = request.UnlockedOnTurn,
            });

            await context.SaveChangesAsync(cancelToken);

            logger.LogInformation(
                "Unlocked slot {SlotId} for player {PlayerId} on turn {Turn}.",
                request.UnitComponentSlotId, request.PlayerId, request.UnlockedOnTurn);

            return ServiceResult<Empty>.SuccessNoContent();
        }
        catch (Exception ex)
        {
            const string error = "An exception occurred while processing " + nameof(UnlockSlotForPlayerHandler) + ".";
            logger.LogError(ex, error);
            return ServiceResult<Empty>.InternalServerError(ex, error);
        }
    }
}
