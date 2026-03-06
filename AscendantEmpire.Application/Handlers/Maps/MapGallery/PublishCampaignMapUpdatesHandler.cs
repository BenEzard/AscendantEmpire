using AscendantEmpire.Application.Common;
using AscendantEmpire.Application.Data;
using AscendantEmpire.Domain.Entities.Games;
using AscendantEmpire.Domain.Enums.Games;
using AscendantEmpire.Domain.Enums.Maps;

using MediatR;

using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.Logging;

using System.Data;

namespace AscendantEmpire.Application.Handlers.Maps.MapGallery;

public record PublishCampaignMapUpdatesRequest(int MapGalleryIndex) : IRequest<ServiceResultBase>;

internal class PublishCampaignMapUpdatesHandler(
    ILogger<PublishCampaignMapUpdatesHandler> logger,
    IDbContextFactory<AscendantEmpireContext> _dbFactory
) : IRequestHandler<PublishCampaignMapUpdatesRequest, ServiceResultBase>
{
    /// <summary>
    /// How many games to update at once.
    /// </summary>
    private const int _gameBatchSize = 10;
    private const int _tileBatchSize = 5_000;

    private readonly record struct GalleryTileDto(
        int X,
        int Y,
        BiomeType BiomeTypeId,
        CampaignMapGeographyType GeographyTypeId
    );

    private readonly record struct CoordKey(int X, int Y);

    public async Task<ServiceResultBase> Handle(PublishCampaignMapUpdatesRequest request, CancellationToken cancellationToken)
    {
        try
        {
            await using AscendantEmpireContext db = await _dbFactory.CreateDbContextAsync(cancellationToken);

            // Load gallery tiles for the requested index into a lookup by (x,y). This is the "source of truth" we’re pushing into games.
            List<GalleryTileDto> galleryTiles = await db.CampaignMapGallery
                .Where(g => g.CampaignMapGalleryIndexId == request.MapGalleryIndex)
                .Select(g => new GalleryTileDto(
                    g.XCoord,
                    g.YCoord,
                    g.BiomeTypeId,
                    g.CampaignMapGeographyTypeId
                ))
                .ToListAsync(cancellationToken);

            if (galleryTiles.Count == 0)
            {
                logger.LogWarning("No map found for CampaignMapGalleryIndexId {IndexId}", request.MapGalleryIndex);
                return ServiceResultBase.NotFound("Unable to find the requested map.");
            }

            Dictionary<CoordKey, GalleryTileDto> galleryByCoord = galleryTiles.ToDictionary(x => new CoordKey(x.X, x.Y));

            // Find eligible games that need updating.
            List<int> eligibleGameIds = await db.GameIndex
                .AsNoTracking()
                .Where(g => g.CampaignMapIndexId == request.MapGalleryIndex)
                .Where(g => g.PlayStatus == GamePlayStatus.InProgress)
                .Select(g => g.Id)
                .ToListAsync(cancellationToken);

            if (eligibleGameIds.Count == 0)
            {
                return ServiceResultBase.SuccessNoContent();
            }

            // Process games in batches (keeps memory + tracking stable)
            foreach (int[] gameIdBatch in eligibleGameIds.Chunk(_gameBatchSize))
            {
                await using IDbContextTransaction transaction = await db.Database.BeginTransactionAsync(cancellationToken);

                await UpdateCampaignMapTilesAsync(
                    db,
                    gameIds: gameIdBatch,
                    galleryTiles: galleryTiles,
                    tileBatchSize: _tileBatchSize,
                    cancellationToken);

                await UpdatePlayerCampaignMapTilesAsync(
                    db,
                    gameIds: gameIdBatch,
                    galleryByCoord: galleryByCoord,
                    cancellationToken);

                await db.SaveChangesAsync(cancellationToken);
                await transaction.CommitAsync(cancellationToken);
                db.ChangeTracker.Clear();
            }

            return ServiceResultBase.SuccessNoContent();
        }
        catch (Exception ex)
        {
            const string error = "An error occurred while publishing Campaign Map updates.";
            logger.LogError(ex, error);
            return ServiceResultBase.InternalServerError(error);
        }
    }

    private static async Task UpdateCampaignMapTilesAsync(
        AscendantEmpireContext db,
        int[] gameIds,
        IReadOnlyList<GalleryTileDto> galleryTiles,
        int tileBatchSize,
        CancellationToken cancellationToken)
    {
        if (gameIds.Length == 0 || galleryTiles.Count == 0)
        {
            return;
        }

        SqlConnection conn = (SqlConnection)db.Database.GetDbConnection();
        if (conn.State != ConnectionState.Open)
        {
            await conn.OpenAsync(cancellationToken);
        }

        SqlTransaction? transaction = db.Database.CurrentTransaction?.GetDbTransaction() as SqlTransaction;

        string gameIdsCsv = string.Join(",", gameIds);

        await using SqlCommand createTempTableCommand = conn.CreateCommand();
        createTempTableCommand.Transaction = transaction;
        createTempTableCommand.CommandText = @"
CREATE TABLE #GalleryTiles (
    XCoord INT NOT NULL,
    YCoord INT NOT NULL,
    BiomeTypeId INT NOT NULL,
    GeographyTypeId INT NOT NULL,
    PRIMARY KEY (XCoord, YCoord)
);";
        await createTempTableCommand.ExecuteNonQueryAsync(cancellationToken);

        try
        {
            DataTable table = new();
            table.Columns.Add("XCoord", typeof(int));
            table.Columns.Add("YCoord", typeof(int));
            table.Columns.Add("BiomeTypeId", typeof(int));
            table.Columns.Add("GeographyTypeId", typeof(int));

            foreach (GalleryTileDto[] tileBatch in galleryTiles.Chunk(tileBatchSize))
            {
                table.Clear();

                foreach (GalleryTileDto tile in tileBatch)
                {
                    table.Rows.Add(tile.X, tile.Y, (int)tile.BiomeTypeId, (int)tile.GeographyTypeId);
                }

                using (SqlBulkCopy bulk = new(conn, SqlBulkCopyOptions.Default, transaction))
                {
                    bulk.DestinationTableName = "#GalleryTiles";
                    await bulk.WriteToServerAsync(table, cancellationToken);
                }

                string sql = $@"
UPDATE cm
SET
    cm.BiomeTypeId = gt.BiomeTypeId,
    cm.CampaignMapGeographyTypeId = gt.GeographyTypeId
FROM game.CampaignMap cm
INNER JOIN #GalleryTiles gt
    ON gt.XCoord = cm.XCoord
    AND gt.YCoord = cm.YCoord
WHERE
    cm.GameId IN ({gameIdsCsv})
    AND (
        cm.BiomeTypeId <> gt.BiomeTypeId
        OR cm.CampaignMapGeographyTypeId <> gt.GeographyTypeId
    );

TRUNCATE TABLE #GalleryTiles;";

                await db.Database.ExecuteSqlRawAsync(sql, cancellationToken);
            }
        }
        finally
        {
            await using SqlCommand dropTempTableCommand = conn.CreateCommand();
            dropTempTableCommand.Transaction = transaction;
            dropTempTableCommand.CommandText = "DROP TABLE #GalleryTiles;";
            await dropTempTableCommand.ExecuteNonQueryAsync(cancellationToken);
        }
    }

    private static async Task UpdatePlayerCampaignMapTilesAsync(
        AscendantEmpireContext db,
        int[] gameIds,
        IReadOnlyDictionary<CoordKey, GalleryTileDto> galleryByCoord,
        CancellationToken cancellationToken)
    {
        if (gameIds.Length == 0 || galleryByCoord.Count == 0)
        {
            return;
        }

        // Pull only rows that are even eligible to change.
        List<PlayerCampaignMap> playerTiles = await db.PlayerCampaignMaps
            .Where(pcm => gameIds.Contains(pcm.GameId))
            .Where(pcm => pcm.BiomeTypeId != BiomeType.Unknown || pcm.CampaignMapGeographyTypeId != CampaignMapGeographyType.Unknown)
            .Join(
                db.Players.Where(p => p.Status == PlayerStatus.Active),
                pcm => pcm.PlayerId,
                p => p.Id,
                (pcm, p) => pcm
            )
            .ToListAsync(cancellationToken);

        foreach (var tile in playerTiles)
        {
            if (!galleryByCoord.TryGetValue(new CoordKey(tile.XCoord, tile.YCoord), out var src))
            {
                continue;
            }

            if (tile.BiomeTypeId != src.BiomeTypeId || tile.CampaignMapGeographyTypeId != src.GeographyTypeId)
            {
                tile.BiomeTypeId = src.BiomeTypeId;
                tile.CampaignMapGeographyTypeId = src.GeographyTypeId;
            }
        }
    }
}
