using AscendantEmpire.Application.Common.Validation;
using AscendantEmpire.Application.Data;
using AscendantEmpire.BackendContracts.MatchMaking;
using AscendantEmpire.Domain.Enums.Games;

using MediatR;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

using static AscendantEmpire.Application.Common.ServiceResultBase;

namespace AscendantEmpire.Application.Features.MatchMaking.Handlers;

/// <summary>
/// Lists the games a user can join, with every active slot flagged as claimed or free.
/// </summary>
/// <remarks>
/// <para>
/// <paramref name="UserId"/> comes from the caller's bearer token, not the request body. It is only used to mark which slots the caller already controls; it does not filter the results.
/// </para>
/// </remarks>
public record GetMatchMakingGamesRequest(int UserId) : IRequest<ServiceResult<IReadOnlyCollection<MatchMakingGame>>>;

internal class GetMatchMakingGamesRequestValidator : Validator<GetMatchMakingGamesRequest>
{
    public GetMatchMakingGamesRequestValidator()
    {
        RuleFor(x => x.UserId)
            .GreaterThan(0).WithMessage("User Id is required.");
    }
}

internal sealed class GetMatchMakingGamesHandler(
    ILogger<GetMatchMakingGamesHandler> logger,
    IDbContextFactory<AscendantEmpireContext> dbFactory
) : IRequestHandler<GetMatchMakingGamesRequest, ServiceResult<IReadOnlyCollection<MatchMakingGame>>>
{
    public async Task<ServiceResult<IReadOnlyCollection<MatchMakingGame>>> Handle(GetMatchMakingGamesRequest request, CancellationToken cancelToken)
    {
        try
        {
            await using AscendantEmpireContext context = await dbFactory.CreateDbContextAsync(cancelToken);

            // Games that have finished setup, whether they are still filling up or already under way.
            // Started games have to stay in this list.
            // Every active slot is returned (claimed and unclaimed) so the UI can show taken vs free seats.
            var rows = await (
                from gi in context.GameIndex.AsNoTracking()
                where gi.CreationStatus == GameCreationStatus.SetupComplete
                   && (gi.PlayStatus == GamePlayStatus.AwaitingPlayers || gi.PlayStatus == GamePlayStatus.InProgress)
                join p in context.Players.AsNoTracking()
                    on gi.Id equals p.GameId
                where p.Status == PlayerStatus.Active
                select new
                {
                    GameId = gi.Id,
                    gi.GameName,
                    gi.NumberOfPlayers,
                    gi.MapWidth,
                    gi.MapHeight,
                    gi.PlayStatus,

                    PlayerId = p.Id,
                    p.RulerTitle,
                    p.RulerName,
                    p.FactionName,
                    p.FactionColour,
                    p.UserId
                }
            ).ToListAsync(cancelToken);

            // Group the flat rows into "game -> slots", deriving the claim flags per slot.
            List<MatchMakingGame> games = [.. rows
                .GroupBy(r => new
                {
                    r.GameId,
                    r.GameName,
                    r.NumberOfPlayers,
                    r.MapWidth,
                    r.MapHeight,
                    r.PlayStatus
                })
                .Select(g => new MatchMakingGame
                {
                    GameId = g.Key.GameId,
                    GameName = g.Key.GameName,
                    NumberOfPlayers = g.Key.NumberOfPlayers,
                    MapWidth = g.Key.MapWidth,
                    MapHeight = g.Key.MapHeight,
                    // Play can only be entered once the lobby has filled, so the UI knows to offer the Play button rather than have it fail.
                    HasStarted = g.Key.PlayStatus == GamePlayStatus.InProgress,
                    Slots = [.. g
                        .Select(s => new MatchMakingSlot
                        {
                            PlayerId = s.PlayerId,
                            RulerTitle = s.RulerTitle,
                            RulerName = s.RulerName,
                            FactionName = s.FactionName,
                            FactionColour = s.FactionColour,
                            RulerLabel = $"{s.RulerTitle} {s.RulerName} ({s.FactionName})",
                            // A slot with a UserId is taken; if that UserId is ours, it's our seat.
                            IsClaimed = s.UserId != null,
                            IsClaimedByCurrentUser = s.UserId == request.UserId
                        })
                        // Ordered by id, not by label: players rename themselves once they claim a seat, and ordering by the label would make the rows jump about between refreshes while a lobby fills.
                        .OrderBy(s => s.PlayerId)]
                })
                .OrderBy(g => g.GameName)];

            return ServiceResult<IReadOnlyCollection<MatchMakingGame>>.Succeed(games);
        }
        catch (Exception ex)
        {
            const string error = "An error occurred while fetching joinable games.";
            logger.LogError(ex, error);
            return ServiceResult<IReadOnlyCollection<MatchMakingGame>>.InternalServerError(error);
        }
    }
}
