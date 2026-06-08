using System.Data;
using System.Text.Json;

using AscendantEmpire.Application.Common;
using AscendantEmpire.Application.Data;
using AscendantEmpire.Application.MapGeneration;
using AscendantEmpire.Domain.Entities.Maps.BattleMaps;
using AscendantEmpire.Domain.Entities.Maps.CampaignMaps;
using AscendantEmpire.Domain.Enums.Maps;

using MediatR;

using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

using static AscendantEmpire.Application.Common.ServiceResultBase;

namespace AscendantEmpire.Application.Features.Maps.BattleMaps;

/// <summary>
/// Lazily generates a BattleMap for a specific campaign tile.
///
/// Called the first time a battle is triggered on a given campaign tile.  If the
/// BattleMap already exists in the database, the handler returns its index Id
/// immediately without regenerating.
///
/// The generated tiles are stored in:
///   mapgallery.BattleMapGalleryIndex — one row describing this BattleMap
///   mapgallery.BattleMapGallery     — one row per battle tile (Width × Height)
///
/// The local seed is derived deterministically:
///   localSeed = masterSeed XOR (tileX * 2654435761) XOR (tileY * 2246822519)
/// so the same BattleMap is always produced for a given campaign map + tile.
/// </summary>
public record GenerateBattleMapRequest(
    int CampaignMapGalleryIndexId,
    int TileX,
    int TileY
) : IRequest<ServiceResult<int>>;

internal sealed class GenerateBattleMapHandler(
    ILogger<GenerateBattleMapHandler> logger,
    AscendantEmpireContext context
) : IRequestHandler<GenerateBattleMapRequest, ServiceResult<int>>
{
    private readonly BattleMapGenerator _generator = new();

    public async Task<ServiceResult<int>> Handle(GenerateBattleMapRequest request, CancellationToken cancellationToken)
    {
        try
        {
            // ── Guard: already generated? ─────────────────────────────────────
            BattleMapGalleryIndex? existing = await context.BattleMapGalleryIndex
                .AsNoTracking()
                .FirstOrDefaultAsync(
                    bmi => bmi.CampaignMapGalleryIndexId == request.CampaignMapGalleryIndexId
                        && bmi.CampaignTileX == request.TileX
                        && bmi.CampaignTileY == request.TileY,
                    cancellationToken);

            if (existing is not null && existing.GenerationStatus == MapCreationStatus.Complete)
            {
                return ServiceResult<int>.Succeed(existing.Id);
            }

            // ── Load the parent campaign map index ────────────────────────────
            CampaignMapGalleryIndex? campaignIndex = await context.CampaignMapGalleryIndex
                .AsNoTracking()
                .FirstOrDefaultAsync(cmi => cmi.Id == request.CampaignMapGalleryIndexId, cancellationToken);

            if (campaignIndex is null)
            {
                return ServiceResult<int>.Fail($"CampaignMapGalleryIndex {request.CampaignMapGalleryIndexId} not found.");
            }

            // ── Parse parameters from PropertiesJson ──────────────────────────
            Dictionary<string, string>? props =JsonSerializer.Deserialize<Dictionary<string, string>>(campaignIndex.PropertiesJson);

            if (props is null)
            {
                return ServiceResult<int>.Fail("CampaignMapGalleryIndex.PropertiesJson is missing or invalid.");
            }

            if (!props.TryGetValue("seed", out var seedStr) || !long.TryParse(seedStr, out long masterSeed))
            {
                return ServiceResult<int>.Fail("Master seed not found in PropertiesJson. Was the campaign map generated with PerlinNoiseV1?");
            }

            int battleMapSize = props.TryGetValue("battleMapSize", out var bmsStr) && int.TryParse(bmsStr, out int bms)
                ? Math.Max(10, bms)
                : 50;

            // ── Load the specific campaign tile ───────────────────────────────
            // CampaignMapGallery rows are ordered Y-then-X from CampaignMapOffsetId.
            // We look up by IndexId + coords rather than deriving the PK, as the offset convention may change in the future.
            CampaignMapGallery? tile = await context.CampaignMapGallery
                .AsNoTracking()
                .FirstOrDefaultAsync(t => t.CampaignMapGalleryIndexId == request.CampaignMapGalleryIndexId
                      && t.XCoord == request.TileX
                      && t.YCoord == request.TileY,
                    cancellationToken);

            if (tile is null)
            {
                return ServiceResult<int>.Fail($"Campaign tile ({request.TileX},{request.TileY}) not found in CampaignMapGallery.");
            }

            CampaignMapGeographyType tileGeo   = tile.CampaignMapGeographyTypeId;
            BiomeType                tileBiome = tile.BiomeTypeId;

            // ── Load orthogonal neighbours' geography types ───────────────────
            // Required so the edge-blending pass can bias altitude near shared edges.
            // Null means no neighbour exists (edge of the campaign map).
            // Order: [Left, Right, Up, Down]
            int[] neighbourDx = [-1, 1, 0,  0];
            int[] neighbourDy = [ 0, 0, -1, 1];

            var neighbourGeos = new CampaignMapGeographyType?[4];
            for (int i = 0; i < 4; i++)
            {
                int nx = request.TileX + neighbourDx[i];
                int ny = request.TileY + neighbourDy[i];

                if (nx < 0 || nx >= campaignIndex.Width || ny < 0 || ny >= campaignIndex.Height)
                {
                    // No neighbour at this edge — leave null.
                    continue;
                }

                CampaignMapGallery? neighbour = await context.CampaignMapGallery
                    .AsNoTracking()
                    .FirstOrDefaultAsync(
                        t => t.CampaignMapGalleryIndexId == request.CampaignMapGalleryIndexId
                          && t.XCoord == nx
                          && t.YCoord == ny,
                        cancellationToken);

                neighbourGeos[i] = neighbour?.CampaignMapGeographyTypeId;
            }

            // ── Run the generator ─────────────────────────────────────────────
            BattleMapGenerator.BattleMapData data = _generator.Generate(
                masterSeed:      masterSeed,
                camX:            request.TileX,
                camY:            request.TileY,
                battleSize:      battleMapSize,
                campaignWidth:   campaignIndex.Width,
                campaignHeight:  campaignIndex.Height,
                tileGeo:         tileGeo,
                tileBiome:       tileBiome,
                neighbourGeo:    neighbourGeos);

            // ── Persist the BattleMapGalleryIndex row ─────────────────────────
            int battleMapIndexId = await InsertBattleMapGalleryIndexAsync(request, battleMapSize, data.LocalSeed, cancellationToken);

            // ── Bulk-insert all BattleMapGallery tiles ────────────────────────
            int tileStartId = await GetNextBattleMapTileStartIdAsync();

            await BulkInsertBattleTilesAsync(battleMapIndexId, tileStartId, battleMapSize, data.Altitudes, data.Biomes);

            // ── Mark the index row as Complete ────────────────────────────────
            await context.BattleMapGalleryIndex
                .Where(bmi => bmi.Id == battleMapIndexId)
                .ExecuteUpdateAsync(set => set
                    .SetProperty(bmi => bmi.GenerationStatus, MapCreationStatus.Complete)
                    .SetProperty(bmi => bmi.Status, "Generation complete")
                    .SetProperty(bmi => bmi.BattleMapOffsetId, tileStartId),
                    cancellationToken);

            return ServiceResult<int>.Succeed(battleMapIndexId);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error generating BattleMap for campaign tile ({TileX},{TileY}) in campaign map {IndexId}.",
                request.TileX, request.TileY, request.CampaignMapGalleryIndexId);

            return ServiceResult<int>.Fail($"BattleMap generation failed: {ex.Message}");
        }
    }

    // ──────────────────────────────────────────────────────────────────────────
    // Database helpers
    // ──────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Inserts a BattleMapGalleryIndex row with status Creating and returns its Id.
    /// Uses an explicit transaction so the row exists before tiles are bulk-inserted.
    /// </summary>
    private async Task<int> InsertBattleMapGalleryIndexAsync(
        GenerateBattleMapRequest request,
        int                      battleMapSize,
        long                     localSeed,
        CancellationToken        cancellationToken)
    {
        var indexRow = new BattleMapGalleryIndex
        {
            CampaignMapGalleryIndexId = request.CampaignMapGalleryIndexId,
            CampaignTileX             = request.TileX,
            CampaignTileY             = request.TileY,
            Width                     = battleMapSize,
            Height                    = battleMapSize,
            GenerationSeed            = localSeed,
            GenerationStatus          = MapCreationStatus.Creating,
            Status                    = "Generation in progress",
            BattleMapOffsetId         = 0,  // updated after tiles are inserted
        };

        context.BattleMapGalleryIndex.Add(indexRow);
        await context.SaveChangesAsync(cancellationToken);

        return indexRow.Id;
    }

    /// <summary>
    /// Determines the next sequential BattleMapGalleryId using a table-level lock
    /// to prevent ID collisions when two BattleMaps are generated concurrently.
    /// </summary>
    private async Task<int> GetNextBattleMapTileStartIdAsync()
    {
        const string sql = """
            SELECT ISNULL(
                (SELECT MAX(BattleMapGalleryId)
                 FROM [mapgallery].[BattleMapGallery] WITH (HOLDLOCK, TABLOCKX)),
                0) + 1
            """;

        await using var conn = new SqlConnection(context.Database.GetConnectionString());
        await conn.OpenAsync();
        await using var cmd = new SqlCommand(sql, conn);
        object result = await cmd.ExecuteScalarAsync() ?? 1;
        return Convert.ToInt32(result);
    }

    /// <summary>
    /// Bulk-inserts all BattleMapGallery tiles using SqlBulkCopy.
    /// Tiles are stored in (Y, X) order — matching the BattleMapOffsetId convention.
    /// </summary>
    private async Task BulkInsertBattleTilesAsync(
        int                  battleMapIndexId,
        int                  startId,
        int                  size,
        BattleMapAltitude[]  altitudes,
        BiomeType[]          biomes)
    {
        var table = new DataTable();
        table.Columns.Add("BattleMapGalleryId",      typeof(int));
        table.Columns.Add("BattleMapGalleryIndexId",  typeof(int));
        table.Columns.Add("XCoord",                   typeof(int));
        table.Columns.Add("YCoord",                   typeof(int));
        table.Columns.Add("AltitudeLevel",            typeof(int));
        table.Columns.Add("BiomeTypeId",              typeof(int));

        int id = startId;
        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                int idx = y * size + x;
                table.Rows.Add(
                    id++,
                    battleMapIndexId,
                    x,
                    y,
                    (int)altitudes[idx],
                    (int)biomes[idx]
                );
            }
        }

        string? connStr = context.Database.GetConnectionString();
        await using var conn = new SqlConnection(connStr);
        await conn.OpenAsync();

        using var bulkCopy = new SqlBulkCopy(conn, SqlBulkCopyOptions.TableLock, null)
        {
            DestinationTableName = "[mapgallery].[BattleMapGallery]",
            BatchSize            = 10_000,
        };

        bulkCopy.ColumnMappings.Add("BattleMapGalleryId",      "BattleMapGalleryId");
        bulkCopy.ColumnMappings.Add("BattleMapGalleryIndexId",  "BattleMapGalleryIndexId");
        bulkCopy.ColumnMappings.Add("XCoord",                   "XCoord");
        bulkCopy.ColumnMappings.Add("YCoord",                   "YCoord");
        bulkCopy.ColumnMappings.Add("AltitudeLevel",            "AltitudeLevel");
        bulkCopy.ColumnMappings.Add("BiomeTypeId",              "BiomeTypeId");

        await bulkCopy.WriteToServerAsync(table);
    }
}
