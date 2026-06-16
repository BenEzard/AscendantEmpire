using AscendantEmpire.Application.Common;
using AscendantEmpire.Application.Data;
using AscendantEmpire.Domain.Entities.Games;
using AscendantEmpire.Domain.Enums.Games;

using FluentValidation;

using MediatR;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

using static AscendantEmpire.Application.Common.ServiceResultBase;

namespace AscendantEmpire.Application.Features.MatchMaking.Handlers;

/// <summary>
/// Assigns an unclaimed player slot to a user, making them the controller of that player.
/// </summary>
/// <remarks>
/// <para>
/// <paramref name="UserId"/> comes from the caller's bearer token, not the request body, so a
/// caller can only ever claim a slot for themselves.
/// </para>
/// <para>
/// A user may hold at most one slot per game (enforced by the <c>UX_Player_Game_User</c> filtered
/// unique index); the explicit checks here just turn that race into a friendly error.
/// </para>
/// </remarks>
public record ClaimPlayerSlotRequest(int UserId, int GameId, int PlayerId) : IRequest<ServiceResult<ServiceResultBase.Empty>>;

internal class ClaimPlayerSlotRequestValidator : AbstractValidator<ClaimPlayerSlotRequest>
{
    public ClaimPlayerSlotRequestValidator()
    {
        RuleFor(x => x.UserId)
            .GreaterThan(0).WithMessage("User Id is required.");

        RuleFor(x => x.GameId)
            .GreaterThan(0).WithMessage("Game Id is required.");

        RuleFor(x => x.PlayerId)
            .GreaterThan(0).WithMessage("Player Id is required.");
    }
}

internal class ClaimPlayerSlotHandler(
    ILogger<ClaimPlayerSlotHandler> logger,
    IDbContextFactory<AscendantEmpireContext> dbFactory
) : IRequestHandler<ClaimPlayerSlotRequest, ServiceResult<Empty>>
{
    public async Task<ServiceResult<Empty>> Handle(ClaimPlayerSlotRequest request, CancellationToken cancelToken)
    {
        try
        {
            await using AscendantEmpireContext context = await dbFactory.CreateDbContextAsync(cancelToken);

            // A user controls at most one player per game, so reject up front if they already hold a
            // slot here — otherwise the unique index would throw when we save.
            bool alreadyInGame = await context.Players
                .AnyAsync(p => p.GameId == request.GameId && p.UserId == request.UserId, cancelToken);

            if (alreadyInGame)
            {
                logger.LogInformation("User {UserId} tried to claim a slot in game {GameId} but already controls a player there.", request.UserId, request.GameId);
                return ServiceResult<Empty>.Conflict("You already control a player in this game.");
            }

            Player? slot = await context.Players
                .FirstOrDefaultAsync(p => p.Id == request.PlayerId && p.GameId == request.GameId, cancelToken);

            if (slot is null)
            {
                logger.LogInformation("User {UserId} tried to claim non-existent slot {PlayerId} in game {GameId}.", request.UserId, request.PlayerId, request.GameId);
                return ServiceResult<Empty>.NotFound("That player slot does not exist.");
            }

            // Only active slots are joinable; an eliminated/banned slot can't be taken over.
            if (slot.Status != PlayerStatus.Active)
            {
                return ServiceResult<Empty>.Conflict("That player slot is not available to claim.");
            }

            if (slot.UserId != null)
            {
                logger.LogInformation("User {UserId} tried to claim slot {PlayerId} in game {GameId}, but it is already taken.", request.UserId, request.PlayerId, request.GameId);
                return ServiceResult<Empty>.Conflict("That player slot has already been claimed.");
            }

            slot.UserId = request.UserId;
            await context.SaveChangesAsync(cancelToken);

            logger.LogInformation("User {UserId} claimed slot {PlayerId} in game {GameId}.", request.UserId, request.PlayerId, request.GameId);

            return ServiceResult<Empty>.SuccessNoContent();
        }
        catch (DbUpdateException ex)
        {
            // Lost a race: the slot was claimed (or the user grabbed another slot in this game)
            // between our checks and the save. The unique index is the real guarantee.
            logger.LogInformation(ex, "Concurrent claim conflict for slot {PlayerId} in game {GameId} by user {UserId}.", request.PlayerId, request.GameId, request.UserId);
            return ServiceResult<Empty>.Conflict("That player slot has already been claimed.");
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "An error occurred while handling {RequestName}.", nameof(ClaimPlayerSlotRequest));
            return ServiceResult<Empty>.InternalServerError("An unexpected error occurred while claiming the player slot.");
        }
    }
}
