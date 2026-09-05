using AscendantEmpire.Application.Common;
using AscendantEmpire.Application.Common.Validation;
using AscendantEmpire.Application.Data;
using AscendantEmpire.Application.Features.Turns.Services;
using AscendantEmpire.Domain.Entities.Games;
using AscendantEmpire.Domain.Enums.Games;

using MediatR;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.Logging;

using static AscendantEmpire.Application.Common.ServiceResultBase;

namespace AscendantEmpire.Application.Features.MatchMaking.Handlers;

/// <summary>
/// Assigns an unclaimed Player slot to a user, making them the controller of that Player.
/// </summary>
/// <remarks>
/// <para>
/// <paramref name="UserId"/> comes from the caller's bearer token, not the request body, so a caller can only ever claim a slot for themselves.
/// </para>
/// <para>
/// A user may hold at most one slot per Game (enforced by the <c>UX_Player_Game_User</c> filtered unique index); the explicit checks here just turn that race into a friendly error.
/// </para>
/// <para>
/// Claiming the <i>last</i> free slot also starts the Game.
/// </para>
/// </remarks>
public record ClaimPlayerSlotRequest(int UserId, int GameId, int PlayerId) : IRequest<ServiceResult<ServiceResultBase.Empty>>;

internal class ClaimPlayerSlotRequestValidator : Validator<ClaimPlayerSlotRequest>
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
    IDbContextFactory<AscendantEmpireContext> dbFactory,
    ICampaignRoundService campaignRoundService
) : IRequestHandler<ClaimPlayerSlotRequest, ServiceResult<Empty>>
{
    public async Task<ServiceResult<Empty>> Handle(ClaimPlayerSlotRequest request, CancellationToken cancelToken)
    {
        try
        {
            await using AscendantEmpireContext context = await dbFactory.CreateDbContextAsync(cancelToken);

            // The claim and the game start that may follow it belong together. Without one transaction around both, a start that failed would leave the seat taken and the game stuck:
            // full, so nothing further could trigger a start, and never begun.
            await using IDbContextTransaction transaction = await context.Database.BeginTransactionAsync(cancelToken);

            // A user controls at most one Player per game, so reject up front if they already hold a slot here — otherwise the unique index would throw when we save.
            bool alreadyInGame = await context.Players
                .AnyAsync(p => p.GameId == request.GameId && p.UserId == request.UserId, cancelToken);

            if (alreadyInGame)
            {
                logger.LogInformation("User {UserId} tried to claim a slot in game {GameId} but already controls a Player there.", request.UserId, request.GameId);
                return ServiceResult<Empty>.Conflict("You already control a Player in this game.");
            }

            Player? slot = await context.Players
                .FirstOrDefaultAsync(p => p.Id == request.PlayerId && p.GameId == request.GameId, cancelToken);

            if (slot is null)
            {
                logger.LogInformation("User {UserId} tried to claim non-existent slot {PlayerId} in game {GameId}.", request.UserId, request.PlayerId, request.GameId);
                return ServiceResult<Empty>.NotFound("That Player slot does not exist.");
            }

            // Only active slots are joinable; an eliminated/banned slot can't be taken over.
            if (slot.Status != PlayerStatus.Active)
            {
                return ServiceResult<Empty>.Conflict("That Player slot is not available to claim.");
            }

            if (slot.UserId != null)
            {
                logger.LogInformation("User {UserId} tried to claim slot {PlayerId} in game {GameId}, but it is already taken.", request.UserId, request.PlayerId, request.GameId);
                return ServiceResult<Empty>.Conflict("That Player slot has already been claimed.");
            }

            slot.UserId = request.UserId;
            await context.SaveChangesAsync(cancelToken);

            logger.LogInformation("User {UserId} claimed slot {PlayerId} in game {GameId}.", request.UserId, request.PlayerId, request.GameId);

            // Was that the last seat? Read it back rather than counting what we thought was free, so a slot claimed by somebody else while this request was in flight is included.
            bool anySlotsLeft = await context.Players
                .AnyAsync(p => p.GameId == request.GameId
                            && p.Status == PlayerStatus.Active
                            && p.UserId == null, cancelToken);

            if (anySlotsLeft == false)
            {
                // The game is full, so play begins. The service is idempotent, so it is harmless if two final claims somehow both get here.
                ServiceResultBase startResult = await campaignRoundService.StartGameAsync(context, request.GameId, cancelToken);

                if (startResult.HasSucceeded == false)
                {
                    // Rolling back un-claims the seat too, which is what makes retrying the claim a real fix rather than a second failure.
                    logger.LogError("Claiming the last slot in game {GameId} failed to start it, so the claim was rolled back. Errors: {Errors}",
                        request.GameId, string.Join("; ", startResult.Errors));

                    return ServiceResult<Empty>.InternalServerError("Your slot was claimed but the game could not be started. Please try again.");
                }
            }

            await transaction.CommitAsync(cancelToken);

            return ServiceResult<Empty>.SuccessNoContent();
        }
        catch (DbUpdateException ex)
        {
            // Lost a race: the slot was claimed (or the user grabbed another slot in this game) between our checks and the save. The unique index is the real guarantee.
            logger.LogInformation(ex, "Concurrent claim conflict for slot {PlayerId} in game {GameId} by user {UserId}.", request.PlayerId, request.GameId, request.UserId);
            return ServiceResult<Empty>.Conflict("That Player slot has already been claimed.");
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "An error occurred while handling {RequestName}.", nameof(ClaimPlayerSlotRequest));
            return ServiceResult<Empty>.InternalServerError("An unexpected error occurred while claiming the Player slot.");
        }
    }
}
