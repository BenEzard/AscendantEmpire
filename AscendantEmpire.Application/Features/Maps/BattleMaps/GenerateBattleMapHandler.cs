using System.Data;
using System.Text.Json;

using AscendantEmpire.Application.Common;
using AscendantEmpire.Application.Common.Messaging;
using AscendantEmpire.Application.Data;
using AscendantEmpire.Application.MapGeneration;
using AscendantEmpire.Domain.Entities.Games;
using AscendantEmpire.Domain.Entities.Maps.BattleMaps;
using AscendantEmpire.Domain.Entities.Maps.CampaignMaps;
using AscendantEmpire.Domain.Enums.Maps;

using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

using static AscendantEmpire.Application.Common.ServiceResultBase;

namespace AscendantEmpire.Application.Features.Maps.BattleMaps;

/// <summary>
/// Lazily generates a BattleMap for a specific campaign tile within a specific game.
///
/// Called the first time a battle is triggered on a given campaign tile.  If the
/// BattleMap already exists in the database, the handler returns its index Id
/// immediately without regenerating.
///
/// The generated tiles are stored in:
///   battle.BattleMapIndex — one row describing this BattleMap (FK to game.CampaignMap)
///   battle.BattleMap      — one row per battle tile (BattleMapSize × BattleMapSize)
///
/// Terrain altitude is derived from the shared campaign Perlin field (using the campaign
/// master seed) so adjacent tiles have continuous terrain edges.  Biome diversity uses
/// GameIndex.GenerationSeed so two games on the same campaign map produce different
/// biome distributions.
/// </summary>
public record GenerateBattleMapRequest(
    int GameId,
    int CampaignMapId
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
            BattleMapIndex? existing = await context.BattleMapIndex
                .AsNoTracking()
                .FirstOrDefaultAsync(
                    bmi => bmi.CampaignMapId == request.CampaignMapId,
                    cancellationToken);

            if (existing is not null)
            {
                if (existing.GenerationStatus == MapCreationStatus.Complete)
                {
                    return ServiceResult<int>.Succeed(existing.Id);
                }

                // An unfinished row is what a generation that failed part-way leaves behind: the index row is inserted as Creating before its tiles are written, and only marked Complete
                // after. Left in place it would make every later attempt collide with the unique index on CampaignMapId, so the tile could never have a battle map at all. Its tiles go
                // first (they may have been written before the Complete update failed), then the row, and the map is generated afresh below.
                //
                // This assumes nothing else is generating the same tile at this moment. Battles only start on the moving player's own step, so two generations of one tile at once is
                // not something play can produce today.
                logger.LogWarning(
                    "Battle Map Index Id {BattleMapIndexId} for Campaign Map Id {CampaignMapId} in game {GameId} was left {GenerationStatus}; clearing it and generating again.",
                    existing.Id,
                    request.CampaignMapId,
                    request.GameId,
                    existing.GenerationStatus);

                await context.BattleMap
                    .Where(tile => tile.BattleMapIndexId == existing.Id)
                    .ExecuteDeleteAsync(cancellationToken);

                await context.BattleMapIndex
                    .Where(index => index.Id == existing.Id)
                    .ExecuteDeleteAsync(cancellationToken);
            }

            // ── Load the game index ───────────────────────────────────────────
            GameIndex? gameIndex = await context.GameIndex
                .AsNoTracking()
                .FirstOrDefaultAsync(g => g.Id == request.GameId, cancellationToken);

            if (gameIndex is null)
            {
                return ServiceResult<int>.Fail($"GameIndex {request.GameId} not found.");
            }

            // ── Load the campaign tile from the game's map ────────────────────
            CampaignMap? tile = await context.CampaignMaps
                .AsNoTracking()
                .FirstOrDefaultAsync(
                    cm => cm.CampaignMapId == request.CampaignMapId && cm.GameId == request.GameId,
                    cancellationToken);

            if (tile is null)
            {
                return ServiceResult<int>.Fail($"CampaignMap {request.CampaignMapId} not found for game {request.GameId}.");
            }

            int tileX = tile.XCoord;
            int tileY = tile.YCoord;

            // ── Load the parent campaign map gallery index ────────────────────
            // Required for the master seed, which drives terrain continuity and lives nowhere else. The map dimensions are deliberately not taken from here: one
            // gallery row is shared by every game copied from it and can be edited afterwards, so the game's own snapshot is what describes this game's map.
            CampaignMapGalleryIndex? campaignIndex = await context.CampaignMapGalleryIndex
                .AsNoTracking()
                .FirstOrDefaultAsync(cmi => cmi.Id == gameIndex.CampaignMapIndexId, cancellationToken);

            if (campaignIndex is null)
            {
                return ServiceResult<int>.Fail($"CampaignMapGalleryIndex {gameIndex.CampaignMapIndexId} not found.");
            }

            // ── Parse the master seed from PropertiesJson ─────────────────────
            Dictionary<string, string>? props = JsonSerializer.Deserialize<Dictionary<string, string>>(campaignIndex.PropertiesJson);

            if (props is null)
            {
                return ServiceResult<int>.Fail("CampaignMapGalleryIndex.PropertiesJson is missing or invalid.");
            }

            if (!props.TryGetValue("seed", out var seedStr) || !long.TryParse(seedStr, out long masterSeed))
            {
                return ServiceResult<int>.Fail("Master seed not found in PropertiesJson. Was the campaign map generated with PerlinNoiseV1?");
            }

            // ── Load orthogonal neighbours' geography from game.CampaignMap ───
            // Required so the edge-blending pass can bias altitude near shared edges.
            // Null means no neighbour exists (edge of the campaign map).
            // Order: [Left, Right, Up, Down]
            int[] neighbourDx = [-1, 1, 0,  0];
            int[] neighbourDy = [ 0, 0, -1, 1];

            var neighbourGeos = new CampaignMapGeographyType?[4];
            for (int i = 0; i < 4; i++)
            {
                int nx = tileX + neighbourDx[i];
                int ny = tileY + neighbourDy[i];

                if (nx < 0 || nx >= gameIndex.MapWidth || ny < 0 || ny >= gameIndex.MapHeight)
                {
                    // No neighbour at this edge — leave null.
                    continue;
                }

                CampaignMap? neighbour = await context.CampaignMaps
                    .AsNoTracking()
                    .FirstOrDefaultAsync(
                        cm => cm.GameId == request.GameId && cm.XCoord == nx && cm.YCoord == ny,
                        cancellationToken);

                neighbourGeos[i] = neighbour?.CampaignMapGeographyTypeId;
            }

            // ── Run the generator ─────────────────────────────────────────────
            BattleMapGenerator.BattleMapData data = _generator.Generate(
                masterSeed:     masterSeed,
                gameSeed:       gameIndex.GenerationSeed,
                camX:           tileX,
                camY:           tileY,
                battleSize:     gameIndex.BattleMapSize,
                campaignWidth:  gameIndex.MapWidth,
                campaignHeight: gameIndex.MapHeight,
                tileGeo:        tile.CampaignMapGeographyTypeId,
                tileBiome:      tile.BiomeTypeId,
                neighbourGeo:   neighbourGeos);

            // ── Persist the BattleMapIndex row ────────────────────────────────
            int battleMapIndexId = await InsertBattleMapIndexAsync(request.CampaignMapId, cancellationToken);

            // ── Bulk-insert all BattleMap tiles ───────────────────────────────
            // Reserving the id range and writing the tiles happen together, inside one transaction, so the range cannot be handed out twice.
            int tileStartId = await BulkInsertBattleTilesAsync(battleMapIndexId, gameIndex.BattleMapSize, data.Altitudes, data.Biomes, cancellationToken);

            // ── Mark the index row as Complete ────────────────────────────────
            await context.BattleMapIndex
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
            logger.LogError(ex, "Error generating BattleMap for CampaignMapId {CampaignMapId} in game {GameId}.",
                request.CampaignMapId, request.GameId);

            return ServiceResult<int>.Fail($"BattleMap generation failed: {ex.Message}");
        }
    }

    // ──────────────────────────────────────────────────────────────────────────
    // Database helpers
    // ──────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Inserts a BattleMapIndex row with status Creating and returns its Id.
    /// Uses an explicit save so the row exists before tiles are bulk-inserted.
    /// </summary>
    private async Task<int> InsertBattleMapIndexAsync(
        int               campaignMapId,
        CancellationToken cancellationToken)
    {
        var indexRow = new BattleMapIndex
        {
            CampaignMapId    = campaignMapId,
            GenerationStatus = MapCreationStatus.Creating,
            Status           = "Generation in progress",
            BattleMapOffsetId = 0,  // updated after tiles are inserted
        };

        context.BattleMapIndex.Add(indexRow);
        await context.SaveChangesAsync(cancellationToken);

        return indexRow.Id;
    }

    /// <summary>
    /// Claims the next block of BattleMap tile ids, holding the table until the caller's transaction ends.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <c>battle.BattleMap.Id</c> is not an IDENTITY column: tile ids are positional, addressing tile (x, y) as <c>BattleMapOffsetId + (y * Size + x)</c>, so the
    /// application chooses them rather than the database. That makes reserving the range this handler is about to write the caller's problem.
    /// </para>
    /// <para>
    /// <c>TABLOCKX</c> with <c>HOLDLOCK</c> keeps the exclusive table lock for the life of the enclosing transaction, which is why the connection and transaction
    /// are passed in rather than opened here. Taken on its own connection with no transaction, as this used to be, the lock was released the moment the SELECT
    /// finished and two concurrent generations could read the same starting id and write overlapping tiles.
    /// </para>
    /// </remarks>
    private static async Task<int> ReserveTileIdRangeAsync(
        SqlConnection connection,
        SqlTransaction transaction,
        CancellationToken cancellationToken)
    {
        const string sql = """
            SELECT ISNULL(
                (SELECT MAX(Id)
                 FROM [battle].[BattleMap] WITH (HOLDLOCK, TABLOCKX)),
                0) + 1
            """;

        await using var command = new SqlCommand(sql, connection, transaction);

        object result = await command.ExecuteScalarAsync(cancellationToken) ?? 1;

        return Convert.ToInt32(result);
    }

    /// <summary>
    /// Reserves this BattleMap's block of tile ids and bulk-inserts its tiles, in one transaction.
    /// </summary>
    /// <remarks>
    /// Tiles are written in (Y, X) order with ids assigned from the reserved start, which is what makes <c>BattleMapOffsetId + (y * Size + x)</c> address a tile.
    /// </remarks>
    /// <returns>The id of the first tile written, to be recorded as the BattleMap's offset.</returns>
    private async Task<int> BulkInsertBattleTilesAsync(
        int                  battleMapIndexId,
        int                  size,
        BattleMapAltitude[]  altitudes,
        BiomeType[]          biomes,
        CancellationToken    cancellationToken)
    {
        await using var connection = new SqlConnection(context.Database.GetConnectionString());
        await connection.OpenAsync(cancellationToken);

        // The reservation and the write share this transaction, so the exclusive table lock taken while reading the highest existing id is still held when the
        // tiles claiming that range are written. Splitting them across two connections, as this used to do, released the lock before a single row was inserted.
        await using SqlTransaction transaction = (SqlTransaction)await connection.BeginTransactionAsync(cancellationToken);

        int startId = await ReserveTileIdRangeAsync(connection, transaction, cancellationToken);

        var table = new DataTable();
        table.Columns.Add("Id",               typeof(int));
        table.Columns.Add("BattleMapIndexId", typeof(int));
        table.Columns.Add("XCoord",           typeof(int));
        table.Columns.Add("YCoord",           typeof(int));
        table.Columns.Add("AltitudeLevel",    typeof(int));
        table.Columns.Add("BiomeTypeId",      typeof(int));

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

        using var bulkCopy = new SqlBulkCopy(connection, SqlBulkCopyOptions.TableLock, transaction)
        {
            DestinationTableName = "[battle].[BattleMap]",
            BatchSize            = 10_000,
        };

        bulkCopy.ColumnMappings.Add("Id",               "Id");
        bulkCopy.ColumnMappings.Add("BattleMapIndexId", "BattleMapIndexId");
        bulkCopy.ColumnMappings.Add("XCoord",           "XCoord");
        bulkCopy.ColumnMappings.Add("YCoord",           "YCoord");
        bulkCopy.ColumnMappings.Add("AltitudeLevel",    "AltitudeLevel");
        bulkCopy.ColumnMappings.Add("BiomeTypeId",      "BiomeTypeId");

        await bulkCopy.WriteToServerAsync(table, cancellationToken);

        await transaction.CommitAsync(cancellationToken);

        return startId;
    }
}
