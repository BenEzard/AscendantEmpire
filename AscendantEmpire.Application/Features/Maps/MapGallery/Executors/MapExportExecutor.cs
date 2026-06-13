using System.IO.Compression;
using System.Text.Json;

using AscendantEmpire.Application.BackgroundServices;
using AscendantEmpire.Application.Data;
using AscendantEmpire.Application.Features.Maps.MapGallery.Classes.Export;
using AscendantEmpire.Application.Options;
using AscendantEmpire.Domain.Entities.BackgroundProcessing;
using AscendantEmpire.Domain.Entities.Maps.CampaignMaps;
using AscendantEmpire.Domain.Enums.Maps;
using AscendantEmpire.Domain.Enums.Processing;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace AscendantEmpire.Application.Features.Maps.MapGallery.Executors;

/// <summary>
/// Executes <see cref="ProcessType.ExportGalleryMap"/> jobs.
/// </summary>
/// <remarks>
/// Builds a zipped JSON export of a campaign map, writes it to the configured temp directory, and records the file path on the <see cref="Processing"/> record 
/// so the API can stream it back to the caller.
/// </remarks>
internal sealed class MapExportExecutor(
    AscendantEmpireContext context,
    IOptions<ExportSettings> exportOptions,
    ILogger<MapExportExecutor> logger
) : IProcessExecutor
{
    private static readonly JsonSerializerOptions _jsonOptions = new()
    {
        WriteIndented = true
    };

    public ProcessType ProcessType => ProcessType.ExportGalleryMap;

    public async Task ExecuteAsync(Processing processing, CancellationToken cancellationToken)
    {
        int processingId = processing.ProcessingId;
        logger.LogInformation("Starting map export for ProcessingId {ProcessingId}.", processingId);

        // Mark as in-progress so the UI can see it has started.
        processing.Status = ProcessingStatus.InProgress;
        processing.StartedAt = DateTime.UtcNow;
        processing.ProgressPercent = 5;
        await context.SaveChangesAsync(cancellationToken);

        try
        {
            int mapGalleryIndexId = ExtractMapGalleryIndexId(processing.MetadataJson);

            // Load the map index record.
            CampaignMapGalleryIndex? index = await context.CampaignMapGalleryIndex
                .AsNoTracking()
                .FirstOrDefaultAsync(x => x.Id == mapGalleryIndexId, cancellationToken);

            if (index is null)
            {
                throw new InvalidOperationException($"CampaignMapGalleryIndex {mapGalleryIndexId} not found.");
            }

            await UpdateProgressAsync(processing, 20, cancellationToken);

            // Load all tiles for this map.
            List<CampaignMapGallery> tiles = await context.CampaignMapGallery
                .AsNoTracking()
                .Where(t => t.CampaignMapGalleryIndexId == mapGalleryIndexId)
                .ToListAsync(cancellationToken);

            await UpdateProgressAsync(processing, 40, cancellationToken);

            List<CampaignMapStartPosition> startPositions = await context.CampaignMapStartPositions
                .AsNoTracking()
                .Where(x => x.CampaignMapGalleryIndexId == mapGalleryIndexId)
                .OrderBy(x => x.YCoord)
                .ThenBy(x => x.XCoord)
                .ThenBy(x => x.Id)
                .ToListAsync(cancellationToken);

            // Determine the distinct biome and geography types referenced by the tiles.
            HashSet<BiomeType> usedBiomeIds = [.. tiles.Select(t => t.BiomeTypeId)];
            HashSet<CampaignMapGeographyType> usedGeoIds = [.. tiles.Select(t => t.CampaignMapGeographyTypeId)];

            List<CampaignBiome> biomes = await context.CampaignBiomes
                .AsNoTracking()
                .Where(b => usedBiomeIds.Contains(b.BiomeTypeId))
                .ToListAsync(cancellationToken);

            List<CampaignGeography> geographies = await context.CampaignGeographies
                .AsNoTracking()
                .Where(g => usedGeoIds.Contains(g.CampaignMapGeographyTypeId))
                .ToListAsync(cancellationToken);

            await UpdateProgressAsync(processing, 60, cancellationToken);

            // Build the export document.
            MapExportDocument document = BuildDocument(index, tiles, biomes, geographies, startPositions);

            await UpdateProgressAsync(processing, 80, cancellationToken);

            // Serialise and zip.
            string zipPath = BuildZipPath(index.MapName, processingId);
            await WriteZipAsync(document, zipPath, cancellationToken);

            await UpdateProgressAsync(processing, 95, cancellationToken);

            // Mark complete.
            processing.Status = ProcessingStatus.Complete;
            processing.ProgressPercent = 100;
            processing.CompletedAt = DateTime.UtcNow;
            processing.ResultReference = zipPath;
            await context.SaveChangesAsync(cancellationToken);

            logger.LogInformation("Map export complete for ProcessingId {ProcessingId}. File: {ZipPath}", processingId, zipPath);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Map export failed for ProcessingId {ProcessingId}.", processingId);

            processing.Status = ProcessingStatus.Failed;
            processing.CompletedAt = DateTime.UtcNow;
            processing.ErrorMessage = ex.Message;

            // Use a non-cancellable token so the failure is persisted even during shutdown.
            await context.SaveChangesAsync(CancellationToken.None);
        }
    }

    private static MapExportDocument BuildDocument(
        CampaignMapGalleryIndex index,
        IEnumerable<CampaignMapGallery> tiles,
        IEnumerable<CampaignBiome> biomes,
        IEnumerable<CampaignGeography> geographies,
        IEnumerable<CampaignMapStartPosition> startPositions)
    {
        // Build tile list ordered by relativeId = Y * Width + X
        List<MapExportTile> exportTiles = [.. tiles
            .OrderBy(t => t.YCoord * index.Width + t.XCoord)
            .Select(t => new MapExportTile
            {
                RelativeId = t.YCoord * index.Width + t.XCoord,
                BiomeTypeId = t.BiomeTypeId,
                GeographyTypeId = t.CampaignMapGeographyTypeId
            })];

        List<MapExportBiome> exportBiomes = [.. biomes
            .OrderBy(b => b.BiomeTypeId)
            .Select(b => new MapExportBiome
            {
                BiomeTypeId = b.BiomeTypeId,
                Name = b.Name,
                MovementCost = b.MovementCost,
                DoesBlockSight = b.DoesBlockSight,
                IsWater = b.IsWater,
                IsTraversable = b.IsTraversable,
                RegenerationRate = b.RegenerationRate,
                IsPaintableOnCampaignMap = b.IsPaintableOnCampaignMap,
                MapEditorHtmlColor = b.MapEditorHtmlColor,
                CanBeElevated = b.CanBeElevated,
                BaseForageValue = b.BaseForageValue,
                BaseHuntValue = b.BaseHuntValue,
                BaseFishValue = b.BaseFishValue,
                BaseFodderValue = b.BaseFodderValue,
                BaseBoneValue = b.BaseBoneValue,
                BaseWoodValue = b.BaseWoodValue,
                BaseStoneValue = b.BaseStoneValue,
                BaseSpecialValue = b.BaseSpecialValue
            })];

        List<MapExportGeography> exportGeographies = [.. geographies
            .OrderBy(g => g.CampaignMapGeographyTypeId)
            .Select(g => new MapExportGeography
            {
                GeographyTypeId = g.CampaignMapGeographyTypeId,
                Name = g.Name,
                RelativeElevation = g.RelativeElevation,
                MovementCost = g.MovementCost,
                MapEditorIcon = g.MapEditorIcon,
                IsPaintableOnCampaignMap = g.IsPaintableOnCampaignMap
            })];

        List<MapExportStartPosition> exportStartPositions = [.. startPositions
            .OrderBy(x => x.YCoord)
            .ThenBy(x => x.XCoord)
            .ThenBy(x => x.Id)
            .Select(x => new MapExportStartPosition
            {
                XCoord = x.XCoord,
                YCoord = x.YCoord,
                DisplayName = x.DisplayName,
                LocationScore = x.LocationScore
            })];

        return new MapExportDocument
        {
            ExportedAt = DateTime.UtcNow,
            Map = new MapExportIndex
            {
                MapName = index.MapName,
                Description = index.Description,
                Width = index.Width,
                Height = index.Height,
                CreationEngine = index.CreationEngine,
                HorizontalWrap = index.HorizontalWrap,
                VerticalWrap = index.VerticalWrap,
                PropertiesJson = index.PropertiesJson,
                CampaignMapOffsetId = index.CampaignMapOffsetId
            },
            Tiles = exportTiles,
            Biomes = exportBiomes,
            Geographies = exportGeographies,
            StartPositions = exportStartPositions
        };
    }

    private static async Task WriteZipAsync(
        MapExportDocument document,
        string zipPath,
        CancellationToken cancellationToken)
    {
        // Ensure directory exists.
        string? directory = Path.GetDirectoryName(zipPath);
        if (!string.IsNullOrEmpty(directory))
        {
            Directory.CreateDirectory(directory);
        }

        string json = JsonSerializer.Serialize(document, _jsonOptions);

        await using FileStream fileStream = new(zipPath, FileMode.Create, FileAccess.Write, FileShare.None);
        using ZipArchive archive = new(fileStream, ZipArchiveMode.Create, leaveOpen: false);

        ZipArchiveEntry entry = archive.CreateEntry("export.json", CompressionLevel.Optimal);
        await using Stream entryStream = entry.Open();
        await using StreamWriter writer = new(entryStream);
        await writer.WriteAsync(json.AsMemory(), cancellationToken);
    }

    private string BuildZipPath(string mapName, int processingId)
    {
        // Sanitise map name for use in a filename.
        string safeName = string.Concat(mapName.Where(c => char.IsLetterOrDigit(c) || c == '_' || c == '-'));
        string fileName = $"export_{safeName}_{processingId}.zip";
        return Path.Combine(exportOptions.Value.TempDirectory, fileName);
    }

    /// <summary>
    /// Persists a progress percentage update to the Processing record.
    /// </summary>
    private async Task UpdateProgressAsync(
        Processing processing,
        byte percent,
        CancellationToken cancellationToken)
    {
        processing.ProgressPercent = percent;
        await context.SaveChangesAsync(cancellationToken);
    }

    /// <summary>
    /// Reads the <c>mapGalleryIndexId</c> value from the Processing record's MetadataJson.
    /// </summary>
    private static int ExtractMapGalleryIndexId(string? metadataJson)
    {
        if (string.IsNullOrWhiteSpace(metadataJson))
        {
            throw new InvalidOperationException("Processing record is missing MetadataJson.");
        }

        using JsonDocument doc = JsonDocument.Parse(metadataJson);
        if (!doc.RootElement.TryGetProperty("mapGalleryIndexId", out JsonElement prop) ||
            !prop.TryGetInt32(out int id))
        {
            throw new InvalidOperationException("MetadataJson does not contain a valid 'mapGalleryIndexId' property.");
        }

        return id;
    }
}
