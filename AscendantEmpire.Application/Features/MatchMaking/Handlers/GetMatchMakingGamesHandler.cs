using AscendantEmpire.Application.Data;
using AscendantEmpire.BackendContracts.MatchMaking;
using AscendantEmpire.Domain.Enums.Games;

using FluentValidation;

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
/// <paramref name="UserId"/> comes from the caller's bearer token, not the request body. It is
/// only used to mark which slots the caller already controls; it does not filter the results.
/// </para>
/// </remarks>
public record GetMatchMakingGamesRequest(int UserId) : IRequest<ServiceResult<IReadOnlyCollection<MatchMakingGame>>>;

internal class GetMatchMakingGamesRequestValidator : AbstractValidator<GetMatchMakingGamesRequest>
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

            // Only games that have finished setup and are in progress are joinable. Every active
            // slot is returned (claimed and unclaimed) so the UI can show taken vs free seats.
            var rows = await (
                from gi in context.GameIndex.AsNoTracking()
                where gi.CreationStatus == GameCreationStatus.SetupComplete
                   && gi.PlayStatus == GamePlayStatus.InProgress
                join cmi in context.CampaignMapGalleryIndex.AsNoTracking()
                    on gi.CampaignMapIndexId equals cmi.Id into cmiGroup
                from cmi in cmiGroup.DefaultIfEmpty()
                join p in context.Players.AsNoTracking()
                    on gi.Id equals p.GameId
                where p.Status == PlayerStatus.Active
                select new
                {
                    GameId = gi.Id,
                    gi.GameName,
                    gi.NumberOfPlayers,
                    cmi.Width,
                    cmi.Height,

                    PlayerId = p.Id,
                    p.RulerTitle,
                    p.RulerName,
                    p.FactionName,
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
                    r.Width,
                    r.Height
                })
                .Select(g => new MatchMakingGame
                {
                    GameId = g.Key.GameId,
                    GameName = g.Key.GameName,
                    NumberOfPlayers = g.Key.NumberOfPlayers,
                    MapWidth = g.Key.Width,
                    MapHeight = g.Key.Height,
                    Slots = [.. g
                        .Select(s => new MatchMakingSlot
                        {
                            PlayerId = s.PlayerId,
                            RulerTitle = s.RulerTitle,
                            RulerName = s.RulerName,
                            FactionName = s.FactionName,
                            RulerLabel = $"{s.RulerTitle} {s.RulerName} ({s.FactionName})",
                            // A slot with a UserId is taken; if that UserId is ours, it's our seat.
                            IsClaimed = s.UserId != null,
                            IsClaimedByCurrentUser = s.UserId == request.UserId
                        })
                        .OrderBy(s => s.RulerLabel)]
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
