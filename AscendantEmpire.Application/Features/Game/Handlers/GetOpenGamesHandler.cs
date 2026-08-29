using AscendantEmpire.Application.Data;
using AscendantEmpire.BackendContracts.Games.GetOpenGames;
using AscendantEmpire.Domain.Enums.Games;

using MediatR;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

using static AscendantEmpire.Application.Common.ServiceResultBase;

namespace AscendantEmpire.Application.Features.Game.Handlers;

/// <summary>
/// Retrieve open games, optionally filtered by game or player identifiers.
/// </summary>
public record GetOpenGamesRequest(int? GameId, int? PlayerId) : IRequest<ServiceResult<IReadOnlyCollection<OpenGame>>>;

internal sealed class GetOpenGamesHandler(
    ILogger<GetOpenGamesHandler> logger,
    AscendantEmpireContext context
) : IRequestHandler<GetOpenGamesRequest, ServiceResult<IReadOnlyCollection<OpenGame>>>
{
    public async Task<ServiceResult<IReadOnlyCollection<OpenGame>>> Handle(GetOpenGamesRequest request, CancellationToken cancelToken)
    {
        try
        {
            if (request.GameId.HasValue && request.GameId <= 0)
            {
                return ServiceResult<IReadOnlyCollection<OpenGame>>.Fail("The specified Game Id is not valid.");
            }
            if (request.PlayerId.HasValue && request.PlayerId <= 0)
            {
                return ServiceResult<IReadOnlyCollection<OpenGame>>.Fail("The specified Player Id is not valid.");
            }

            var rows = await (
                from gi in context.GameIndex.AsNoTracking()
                where gi.CreationStatus == GameCreationStatus.SetupComplete
                   && gi.PlayStatus == GamePlayStatus.InProgress
                join p in context.Players.AsNoTracking()
                    on gi.Id equals p.GameId
                where (request.GameId.HasValue == false || gi.Id == request.GameId)
                where p.Status == PlayerStatus.Active
                where (request.PlayerId.HasValue == false || p.Id == request.PlayerId)
                select new
                {
                    GameId = gi.Id,
                    gi.GameName,
                    gi.NumberOfPlayers,
                    gi.MapWidth,
                    gi.MapHeight,
                    gi.CampaignMapOffsetId,
                    gi.HorizontalWrap,
                    gi.VerticalWrap,

                    PlayerId = p.Id,
                    p.RulerTitle,
                    p.RulerName,
                    p.FactionName,
                    p.CampaignOffsetId
                }
            ).ToListAsync(cancelToken);

            // Group into "game -> players"
            List<OpenGame> openGames = [.. rows
                .GroupBy(r => new
                {
                    r.GameId,
                    r.GameName,
                    r.NumberOfPlayers,
                    r.MapWidth,
                    r.MapHeight,
                    r.CampaignMapOffsetId,
                    r.HorizontalWrap,
                    r.VerticalWrap
                })
                .Select(g => new OpenGame
                {
                    GameId = g.Key.GameId,
                    GameName = g.Key.GameName,
                    NumberOfPlayers = g.Key.NumberOfPlayers,
                    MapWidth = g.Key.MapWidth,
                    MapHeight = g.Key.MapHeight,
                    CampaignMapOffsetId = g.Key.CampaignMapOffsetId,
                    HasHorizontalWrapping = g.Key.HorizontalWrap,
                    HasVerticalWrapping = g.Key.VerticalWrap,
                    Players = [.. g
                        .Select(p => new OpenGamePlayer
                        {
                            PlayerId = p.PlayerId,
                            RulerTitle = p.RulerTitle,
                            RulerName = p.RulerName,
                            FactionName = p.FactionName,
                            RulerLabel = $"{p.RulerTitle} {p.RulerName} ({p.FactionName})",
                            CampaignMapOffsetId = p.CampaignOffsetId
                        })
                        .OrderBy(p => p.RulerLabel)]
                })
                .OrderBy(g => g.GameName)];

            return ServiceResult<IReadOnlyCollection<OpenGame>>.Succeed(openGames);
        }
        catch (Exception ex)
        {
            const string error = "An error occurred while fetching open games.";
            logger.LogError(ex, error);
            return ServiceResult<IReadOnlyCollection<OpenGame>>.Fail(error);
        }
    }
}
