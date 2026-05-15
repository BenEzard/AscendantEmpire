using AscendantEmpire.Application.Data;
using AscendantEmpire.BackendContracts.Games.GetOpenGames;
using AscendantEmpire.Domain.Enums.Games;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

using static AscendantEmpire.Application.Common.ServiceResultBase;

namespace AscendantEmpire.Application.Features.Game.Services;

internal class OpenGameService(
    ILogger<OpenGameService> logger,
    IDbContextFactory<AscendantEmpireContext> dbFactory) : IOpenGameService
{
    /// <inheritdoc />
    public async Task<ServiceResult<IReadOnlyCollection<OpenGame>>> GetOpenGames(int? gameId, int? playerId, CancellationToken cancelToken)
    {
        try
        {
            if (gameId.HasValue && gameId <= 0)
            {
                return ServiceResult<IReadOnlyCollection<OpenGame>>.Fail("The specified Game Id is not valid.");
            }
            if (playerId.HasValue && playerId <= 0)
            {
                return ServiceResult<IReadOnlyCollection<OpenGame>>.Fail("The specified Player Id is not valid.");
            }

            await using AscendantEmpireContext context = await dbFactory.CreateDbContextAsync(cancelToken);

            var rows = await (
                from gi in context.GameIndex.AsNoTracking()
                where gi.CreationStatus == GameCreationStatus.SetupComplete
                   && gi.PlayStatus == GamePlayStatus.InProgress
                join cmi in context.CampaignMapGalleryIndex.AsNoTracking()
                    on gi.CampaignMapIndexId equals cmi.Id into cmiGroup
                from cmi in cmiGroup.DefaultIfEmpty()
                join p in context.Players.AsNoTracking()
                    on gi.Id equals p.GameId
                where (gameId.HasValue == false || gi.Id == gameId)
                where p.Status == PlayerStatus.Active
                where (playerId.HasValue == false || p.Id == playerId)
                select new
                {
                    GameId = gi.Id,
                    gi.GameName,
                    gi.NumberOfPlayers,
                    cmi.Width,
                    cmi.Height,
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
                    r.Width,
                    r.Height,
                    r.HorizontalWrap,
                    r.VerticalWrap
                })
                .Select(g => new OpenGame
                {
                    GameId = g.Key.GameId,
                    GameName = g.Key.GameName,
                    NumberOfPlayers = g.Key.NumberOfPlayers,
                    MapWidth = g.Key.Width,
                    MapHeight = g.Key.Height,
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
