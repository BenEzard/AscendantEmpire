using AscendantEmpire.Application.Common;
using AscendantEmpire.Application.Common.Validation;
using AscendantEmpire.Application.Data;
using AscendantEmpire.Application.Features.Authentication.Classes;
using AscendantEmpire.Application.Features.Authentication.Services;
using AscendantEmpire.Domain.Entities.Games;
using AscendantEmpire.Domain.Enums.Authentication;
using AscendantEmpire.Domain.Enums.Games;

using MediatR;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

using static AscendantEmpire.Application.Common.ServiceResultBase;

namespace AscendantEmpire.Application.Features.Authentication.Handlers;

/// <summary>
/// Resolves which Player the given user controls in the given game, so the API can issue a
/// game-scoped token. <paramref name="UserId"/> comes from the caller's existing user token,
/// not from the request body.
/// </summary>
public record EnterGameRequest(
    int UserId,
    int GameId
) : IRequest<ServiceResult<GameContext>>;

internal class EnterGameRequestValidator : Validator<EnterGameRequest>
{
    public EnterGameRequestValidator()
    {
        RuleFor(x => x.UserId)
            .GreaterThan(0).WithMessage("User Id is required.");

        RuleFor(x => x.GameId)
            .GreaterThan(0).WithMessage("Game Id is required.");
    }
}

internal class EnterGameHandler(
    ILogger<EnterGameHandler> logger,
    IDbContextFactory<AscendantEmpireContext> dbFactory
) : IRequestHandler<EnterGameRequest, ServiceResult<GameContext>>
{
    public async Task<ServiceResult<GameContext>> Handle(EnterGameRequest request, CancellationToken cancelToken)
    {
        try
        {
            await using AscendantEmpireContext context = await dbFactory.CreateDbContextAsync(cancelToken);

            // A user controls at most one Player per game (enforced by the UX_Player_Game_User
            // unique index), so this single lookup fully determines their game context.
            Player? player = await context.Players
                .Where(p => p.GameId == request.GameId && p.UserId == request.UserId)
                .FirstOrDefaultAsync(cancelToken);

            if (player is null)
            {
                logger.LogInformation("User {UserId} attempted to enter game {GameId} but controls no player there.", request.UserId, request.GameId);
                return ServiceResult<GameContext>.Forbidden("You are not a player in that game.");
            }

            // A game still filling its slots has nobody taking a turn and no turn clock running, so there is nothing to enter yet. Refusing
            // here rather than issuing a token keeps the player in the Match Maker, where they can watch the remaining seats fill.
            GamePlayStatus playStatus = await context.GameIndex
                .Where(g => g.Id == request.GameId)
                .Select(g => g.PlayStatus)
                .FirstAsync(cancelToken);

            if (playStatus == GamePlayStatus.AwaitingPlayers)
            {
                logger.LogInformation("User {UserId} attempted to enter game {GameId}, which has not started yet.", request.UserId, request.GameId);
                return ServiceResult<GameContext>.Conflict("That game hasn't started yet - it's still waiting for players.");
            }

            // RulerName is a reasonable display label; the username is what the token cares about.
            // We re-read the username from the user record the token already proved, so we don't
            // need to trust anything from the request body.
            string username = await context.Users
                .Where(u => u.Id == request.UserId)
                .Select(u => u.Username)
                .FirstAsync(cancelToken);

            logger.LogInformation("User {UserId} entered game {GameId} as player {PlayerId}.", request.UserId, request.GameId, player.Id);

            // Permissions belong to the account, so the game token carries the same set the user token did.
            IReadOnlyCollection<PermissionType> permissions = await context.GetUserPermissionsAsync(request.UserId, cancelToken);

            return ServiceResult<GameContext>.Succeed(new GameContext(request.UserId, username, request.GameId, player.Id, permissions));
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "An error occurred while handling {RequestName}.", nameof(EnterGameRequest));
            return ServiceResult<GameContext>.InternalServerError("An unexpected error occurred while entering the game.");
        }
    }
}
