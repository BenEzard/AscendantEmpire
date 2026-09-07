using AscendantEmpire.Application.Data;
using AscendantEmpire.BackendContracts.Games.GetGameContext;
using AscendantEmpire.Domain.Enums.Games;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

using static AscendantEmpire.Application.Common.ServiceResultBase;

namespace AscendantEmpire.Application.Features.Game.Services;

internal class GameContextService(
    ILogger<GameContextService> logger,
    IDbContextFactory<AscendantEmpireContext> dbFactory) : IGameContextService
{
    /// <inheritdoc />
    public async Task<ServiceResult<IReadOnlyCollection<GameContext>>> GetGameContexts(int? gameId, int? playerId, CancellationToken cancelToken)
    {
        try
        {
            if (gameId.HasValue && gameId <= 0)
            {
                return ServiceResult<IReadOnlyCollection<GameContext>>.Fail("The specified Game Id is not valid.");
            }
            if (playerId.HasValue && playerId <= 0)
            {
                return ServiceResult<IReadOnlyCollection<GameContext>>.Fail("The specified Player Id is not valid.");
            }

            await using AscendantEmpireContext context = await dbFactory.CreateDbContextAsync(cancelToken);

            var rows = await (
                from gi in context.GameIndex.AsNoTracking()
                where gi.CreationStatus == GameCreationStatus.SetupComplete
                   && gi.PlayStatus == GamePlayStatus.InProgress
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
            List<GameContext> gameContexts = [.. rows
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
                .Select(g => new GameContext
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
                        .Select(p => new GameContextPlayer
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

            return ServiceResult<IReadOnlyCollection<GameContext>>.Succeed(gameContexts);
        }
        catch (Exception ex)
        {
            const string error = "An error occurred while fetching game contexts.";
            logger.LogError(ex, error);
            return ServiceResult<IReadOnlyCollection<GameContext>>.Fail(error);
        }
    }
}
