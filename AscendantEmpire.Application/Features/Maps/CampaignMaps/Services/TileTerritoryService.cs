using AscendantEmpire.Application.Common;
using AscendantEmpire.Application.Data;
using AscendantEmpire.Application.Features.Game.Services;
using AscendantEmpire.Application.Features.Maps.CampaignMaps.Classes;
using AscendantEmpire.BackendContracts.Games.GetGameContext;
using AscendantEmpire.Domain.Entities.Games;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

using static AscendantEmpire.Application.Common.ServiceResultBase;

namespace AscendantEmpire.Application.Features.Maps.CampaignMaps.Services;

/// <inheritdoc cref="ITileTerritoryService" />
internal class TileTerritoryService(
    ILogger<TileTerritoryService> logger,
    IDbContextFactory<AscendantEmpireContext> dbFactory,
    ICampaignTerritoryReader campaignTerritoryReader,
    IGameContextService gameContextService) : ITileTerritoryService
{
    /// <inheritdoc />
    public async Task<ServiceResult<TerritoryChanges>> ResolveGameTerritoryAsync(int gameId, CancellationToken cancelToken = default)
    {
        try
        {
            await using AscendantEmpireContext context = await dbFactory.CreateDbContextAsync(cancelToken);

            ServiceResult<TerritoryChanges> result = await ResolveGameTerritoryAsync(context, gameId, cancelToken);

            if (result.HasSucceeded && result.SuccessValue?.IsEmpty == false)
            {
                // Ours to save, because this overload opened the context.
                await context.SaveChangesAsync(cancelToken);
            }

            return result;
        }
        catch (Exception ex)
        {
            const string error = "An error occurred while resolving a Game's tile territory.";
            logger.LogError(ex, "{Error} Game Id {GameId}.", error, gameId);

            return ServiceResult<TerritoryChanges>.Fail(error + " Exception: " + ex.Message);
        }
    }

    /// <inheritdoc />
    public async Task<ServiceResult<TerritoryChanges>> ResolveGameTerritoryAsync(AscendantEmpireContext context, int gameId, CancellationToken cancelToken = default)
    {
        ArgumentNullException.ThrowIfNull(context);

        try
        {
            ServiceResult<IReadOnlyCollection<GameContext>> gameContexts = await gameContextService.GetGameContexts(gameId, playerId: null, cancelToken);

            if (gameContexts.HasSucceeded == false)
            {
                return ServiceResult<TerritoryChanges>.Fail(gameContexts.Errors);
            }

            GameContext? game = gameContexts.SuccessValue?.FirstOrDefault();

            if (game is null)
            {
                // Not an error: a Game that is not in progress has no territory to resolve, which is the same answer as nothing having changed.
                return ServiceResult<TerritoryChanges>.Succeed(TerritoryChanges.Empty);
            }

            CampaignMapGeometry geometry = CampaignMapGeometry.From(game);

            IReadOnlyCollection<TerritoryTile> tiles = await campaignTerritoryReader.GetTerritoryAsync(context, gameId, cancelToken);

            if (tiles.Count == 0)
            {
                return ServiceResult<TerritoryChanges>.Succeed(TerritoryChanges.Empty);
            }

            IReadOnlyCollection<int> playerIds = await campaignTerritoryReader.GetPlayerIdsInTurnOrderAsync(context, gameId, cancelToken);

            TerritoryChanges changes = TileTerritory.Resolve(TerritoryMap.Create(geometry, tiles), playerIds);

            if (changes.IsEmpty)
            {
                return ServiceResult<TerritoryChanges>.Succeed(changes);
            }

            ServiceResultBase applied = await ApplyAsync(context, gameId, changes, cancelToken);

            return applied.HasSucceeded
                ? ServiceResult<TerritoryChanges>.Succeed(changes)
                : ServiceResult<TerritoryChanges>.Fail(applied.Errors);
        }
        catch (Exception ex)
        {
            const string error = "An error occurred while resolving a Game's tile territory.";
            logger.LogError(ex, "{Error} Game Id {GameId}.", error, gameId);

            return ServiceResult<TerritoryChanges>.Fail(error + " Exception: " + ex.Message);
        }
    }

    /// <inheritdoc />
    public async Task<ServiceResultBase> ApplyAsync(AscendantEmpireContext context, int gameId, TerritoryChanges changes, CancellationToken cancelToken = default)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(changes);

        if (changes.IsEmpty)
        {
            return SuccessNoContent();
        }

        try
        {
            await WriteTilesAsync(context, gameId, changes, cancelToken);

            WriteClaims(context, gameId, changes);

            return SuccessNoContent();
        }
        catch (Exception ex)
        {
            const string error = "An error occurred while writing a Game's tile territory.";
            logger.LogError(ex, "{Error} Game Id {GameId}.", error, gameId);

            return Fail(error + " Exception: " + ex.Message);
        }
    }

    /// <summary>
    /// Stages the ownership columns and the <c>HasClaims</c> flag on the tiles that changed.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Tracked entity writes and one <c>SaveChangesAsync</c>, as <c>RegenerateTileSustenance</c> does, because the ordinary delta is a handful of tiles and EF batches
    /// the statements anyway. <c>ExecuteUpdateAsync</c> can only set a constant, so it would need the writes grouped by owner - worth doing if a large enclosure ever
    /// measures slow, and a change confined to this method.
    /// </para>
    /// <para>
    /// Only the changed tiles are loaded. A tile named by <c>HasClaimsWrites</c> alone is loaded too, which is how a stale flag gets repaired even when nothing else
    /// about the tile moved.
    /// </para>
    /// </remarks>
    private static async Task WriteTilesAsync(AscendantEmpireContext context, int gameId, TerritoryChanges changes, CancellationToken cancelToken)
    {
        int[] tileIds = [.. changes.OwnershipWrites.Keys.Union(changes.HasClaimsWrites.Keys)];

        if (tileIds.Length == 0)
        {
            return;
        }

        List<CampaignMap> tiles = await context.CampaignMaps
            .Where(tile => tile.GameId == gameId && tileIds.Contains(tile.CampaignMapId))
            .ToListAsync(cancelToken);

        foreach (CampaignMap tile in tiles)
        {
            if (changes.OwnershipWrites.TryGetValue(tile.CampaignMapId, out TileOwnershipWrite? ownership))
            {
                tile.OwnedByPlayerId = ownership.OwnedByPlayerId;
                tile.OwnedBySettlementId = ownership.OwnedBySettlementId;

                if (ownership.ClearsAbandonment)
                {
                    // Taking the ground extinguishes what it remembered: an Abandoned tile that is owned again is no longer abandoned by anybody.
                    tile.LastOwnedByPlayerId = null;
                    tile.AbandonedOnTurnId = null;
                }
            }

            if (changes.HasClaimsWrites.TryGetValue(tile.CampaignMapId, out bool hasClaims))
            {
                tile.HasClaims = hasClaims;
            }
        }
    }

    /// <summary>
    /// Stages the claim junction's inserts and deletes.
    /// </summary>
    /// <remarks>
    /// Deletes go in as stub entities keyed by the composite primary key, which needs no read. That is safe because the reader is <c>AsNoTracking</c>, so none of these
    /// rows is already tracked and a stub cannot collide with a tracked instance - which is load-bearing rather than incidental. It also keeps every test of this
    /// service off the containerised tier, since nothing here needs <c>ExecuteDelete</c> or raw SQL.
    /// </remarks>
    private static void WriteClaims(AscendantEmpireContext context, int gameId, TerritoryChanges changes)
    {
        if (changes.ClaimsToAdd.Count > 0)
        {
            context.CampaignMapClaims.AddRange(changes.ClaimsToAdd.Select(claim => new CampaignMapClaim
            {
                CampaignMapId = claim.CampaignMapId,
                PlayerId = claim.PlayerId,
                GameId = gameId
            }));
        }

        if (changes.ClaimsToRemove.Count > 0)
        {
            context.CampaignMapClaims.RemoveRange(changes.ClaimsToRemove.Select(claim => new CampaignMapClaim
            {
                CampaignMapId = claim.CampaignMapId,
                PlayerId = claim.PlayerId,
                GameId = gameId
            }));
        }
    }
}
