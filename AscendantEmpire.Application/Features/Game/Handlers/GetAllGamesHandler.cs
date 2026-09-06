using AscendantEmpire.Application.Data;
using AscendantEmpire.BackendContracts.Games.GetGames;
using AscendantEmpire.Domain.Enums.Games;

using MediatR;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

using static AscendantEmpire.Application.Common.ServiceResultBase;

namespace AscendantEmpire.Application.Features.Game.Handlers;

/// <summary>
/// Lists every Game in the database, whatever state it is in, for the System panel's administrative Games page.
/// </summary>
/// <remarks>
/// <para>
/// Deliberately unfiltered, and deliberately separate from <c>GetMatchMakingGamesRequest</c> and <c>GetOpenGamesRequest</c>. Those two answer "what can I
/// join?" and so hide games that failed setup, are still being built, or have been abandoned. Those are precisely the games an administrator wants to see,
/// because they are the ones most likely to want deleting.
/// </para>
/// <para>
/// The request carries no parameters, and no user identity: the endpoint is gated on the <c>DeleteGame</c> permission, and the answer is the same for
/// everyone who holds it.
/// </para>
/// </remarks>
public record GetAllGamesRequest() : IRequest<ServiceResult<IReadOnlyCollection<GameSummary>>>;

internal sealed class GetAllGamesHandler(
    ILogger<GetAllGamesHandler> logger,
    IDbContextFactory<AscendantEmpireContext> dbFactory
) : IRequestHandler<GetAllGamesRequest, ServiceResult<IReadOnlyCollection<GameSummary>>>
{
    public async Task<ServiceResult<IReadOnlyCollection<GameSummary>>> Handle(GetAllGamesRequest request, CancellationToken cancelToken)
    {
        try
        {
            await using AscendantEmpireContext context = await dbFactory.CreateDbContextAsync(cancelToken);

            // NumberOfPlayers is the slot count the game was set up with; the claimed count is how many of those slots someone is actually sitting in.
            // A slot is claimed when it carries a UserId, and only active players count - an eliminated or banned player is not occupying a seat that
            // anyone else could take.
            List<GameSummary> games = await context.GameIndex
                .AsNoTracking()
                .Select(gi => new GameSummary
                {
                    GameId = gi.Id,
                    GameName = gi.GameName,
                    NumberOfPlayers = gi.NumberOfPlayers,
                    ClaimedPlayerCount = context.Players.Count(p => p.GameId == gi.Id && p.UserId != null && p.Status == PlayerStatus.Active),
                    CreationStatus = gi.CreationStatus,
                    PlayStatus = gi.PlayStatus,
                    CreatedAt = gi.CreatedAt
                })
                // Newest first: a game someone has just broken is the one they are most likely to have come here to delete.
                .OrderByDescending(g => g.CreatedAt)
                .ThenByDescending(g => g.GameId)
                .ToListAsync(cancelToken);

            return ServiceResult<IReadOnlyCollection<GameSummary>>.Succeed(games);
        }
        catch (Exception ex)
        {
            const string error = "An error occurred while fetching the list of games.";
            logger.LogError(ex, error);
            return ServiceResult<IReadOnlyCollection<GameSummary>>.InternalServerError(error);
        }
    }
}
