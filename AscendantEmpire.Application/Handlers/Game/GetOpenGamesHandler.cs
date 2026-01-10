using AscendantEmpire.Application.Data;
using AscendantEmpire.BackendContracts.Games.GetOpenGames;
using AscendantEmpire.Domain.Enums.Games;

using MediatR;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

using static AscendantEmpire.Application.Common.ServiceResultBase;

namespace AscendantEmpire.Application.Handlers.Game;

/// <summary>
/// Retrieve open games, optionally filtered by game or player identifiers.
/// </summary>
public record GetOpenGamesRequest(Guid? GameGuid, Guid? PlayerGuid) : IRequest<ServiceResult<IReadOnlyCollection<OpenGame>>>;

internal sealed class GetOpenGamesHandler(
    ILogger<GetOpenGamesHandler> logger,
    AscendantEmpireContext context
) : IRequestHandler<GetOpenGamesRequest, ServiceResult<IReadOnlyCollection<OpenGame>>>
{
    public async Task<ServiceResult<IReadOnlyCollection<OpenGame>>> Handle(GetOpenGamesRequest request, CancellationToken cancelToken)
    {
        try
        {
            if (request.GameGuid.HasValue && request.GameGuid is not Guid)
            {
                return ServiceResult<IReadOnlyCollection<OpenGame>>.Fail("The specified Game parameter was not of the required type.");
            }
            if (request.PlayerGuid.HasValue && request.PlayerGuid is not Guid)
            {
                return ServiceResult<IReadOnlyCollection<OpenGame>>.Fail("The specified Player parameter was not of the required type.");
            }

            var rows = await (
                from gi in context.GameIndex.AsNoTracking()
                where gi.CreationStatus == GameCreationStatus.SetupComplete
                   && gi.PlayStatus == GamePlayStatus.InProgress
                join cmi in context.CampaignMapGalleryIndex.AsNoTracking()
                    on gi.CampaignMapIndexId equals cmi.Id into cmiGroup
                from cmi in cmiGroup.DefaultIfEmpty()
                join p in context.Players.AsNoTracking()
                    on gi.Id equals p.GameId
                where (request.GameGuid.HasValue == false || gi.GameGuid == request.GameGuid)
                where p.Status == PlayerStatus.Active
                where (request.PlayerGuid.HasValue == false || p.Guid == request.PlayerGuid)
                select new
                {
                    GameId = gi.Id,
                    gi.GameName,
                    gi.GameGuid,
                    gi.NumberOfPlayers,
                    Width = cmi.Width,
                    Height = cmi.Height,
                    gi.HorizontalWrap,
                    gi.VerticalWrap,

                    PlayerId = p.Id,
                    PlayerGuid = p.Guid,
                    p.RulerTitle,
                    p.RulerName,
                    p.FactionName,
                    p.CampaignOffsetId
                }
            ).ToListAsync(cancelToken);

            // Group into "game -> players"
            List<OpenGame> openGames = rows
                .GroupBy(r => new
                {
                    r.GameId,
                    r.GameName,
                    r.GameGuid,
                    r.NumberOfPlayers,
                    r.Width,
                    r.Height,
                    r.HorizontalWrap,
                    r.VerticalWrap
                })
                .Select(g => new OpenGame
                {
                    GameId = g.Key.GameId,
                    GameName = g.Key.GameName,
                    GameGuid = g.Key.GameGuid,
                    NumberOfPlayers = g.Key.NumberOfPlayers,
                    MapWidth = g.Key.Width,
                    MapHeight = g.Key.Height,
                    HasHorizontalWrapping = g.Key.HorizontalWrap,
                    HasVerticalWrapping = g.Key.VerticalWrap,
                    Players = g
                        .Select(p => new OpenGamePlayer
                        {
                            PlayerId = p.PlayerId,
                            PlayerGuid = p.PlayerGuid,
                            RulerTitle = p.RulerTitle,
                            RulerName = p.RulerName,
                            FactionName = p.FactionName,
                            RulerLabel = $"{p.RulerTitle} {p.RulerName} ({p.FactionName})",
                            CampaignMapOffsetId = p.CampaignOffsetId
                        })
                        .OrderBy(p => p.RulerLabel)
                        .ToList()
                })
                .OrderBy(g => g.GameName)
                .ToList();

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
