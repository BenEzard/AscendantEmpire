using System.IO.Compression;
using System.Text.Json;

using AscendantEmpire.Application.BackgroundServices;
using AscendantEmpire.Application.Data;
using AscendantEmpire.Application.Features.Maps.MapGallery.Classes.Export;
using AscendantEmpire.Application.Helpers.ScaledNumerics;
using AscendantEmpire.Domain.Entities.BackgroundProcessing;
using AscendantEmpire.Domain.Entities.Maps.CampaignMaps;
using AscendantEmpire.Domain.Enums.Maps;
using AscendantEmpire.Domain.Enums.Processing;
using AscendantEmpire.Domain.Enums.System;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.Logging;

namespace AscendantEmpire.Application.Features.Maps.MapGallery.Executors;

/// <summary>
/// Executes <see cref="ProcessType.ImportGalleryMap"/> jobs — the inverse of <see cref="MapExportExecutor"/>.
/// </summary>
/// <remarks>
/// Reads the zipped <c>export.json</c> staged on disk by the import endpoint, ensures the referenced
/// biomes/geographies exist (inserting only the missing ones), then inserts a brand-new map (index, tiles
/// and start positions). Finally it enqueues a <see cref="ProcessType.RenderCampaignMapPreview"/> job so
/// the preview PNGs are regenerated locally (the bundled previews are not portable across machines).
/// </remarks>
internal sealed class MapImportExecutor(
    AscendantEmpireContext context,
    IBackgroundProcessingChannel processingChannel,
    ILogger<MapImportExecutor> logger
) : IProcessExecutor
{
    private static readonly JsonSerializerOptions _jsonOptions = new()
    {
        // The export writes PascalCase property names and numeric enums; be lenient on casing when reading back.
        PropertyNameCaseInsensitive = true
    };

    public ProcessType ProcessType => ProcessType.ImportGalleryMap;

    public async Task ExecuteAsync(Processing processing, CancellationToken cancellationToken)
    {
        int processingId = processing.ProcessingId;
        logger.LogInformation("Starting map import for ProcessingId {ProcessingId}.", processingId);

        // Mark as in-progress so the UI can see it has started.
        processing.Status = ProcessingStatus.InProgress;
        processing.StartedAt = DateTime.UtcNow;
        processing.ProgressPercent = 5;
        await context.SaveChangesAsync(cancellationToken);

        string zipPath = ExtractImportZipPath(processing.MetadataJson);

        try
        {
            // --- Read and parse the export document from the staged zip -------------------
            MapExportDocument document = ReadDocument(zipPath);

            await UpdateProgressAsync(processing, 15, cancellationToken);

            // --- Biomes (insert only the ones that don't already exist) -------------------
            await ImportMissingBiomesAsync(document, cancellationToken);
            await UpdateProgressAsync(processing, 30, cancellationToken);

            // --- Geographies (insert only the ones that don't already exist) --------------
            await ImportMissingGeographiesAsync(document, cancellationToken);
            await UpdateProgressAsync(processing, 45, cancellationToken);

            // --- Index --------------------------------------------------------------------
            CampaignMapGalleryIndex newIndex = await CreateIndexAsync(document, cancellationToken);
            await UpdateProgressAsync(processing, 60, cancellationToken);

            // --- Tiles --------------------------------------------------------------------
            await ImportTilesAsync(document, newIndex, cancellationToken);
            await UpdateProgressAsync(processing, 85, cancellationToken);

            // --- Start positions ----------------------------------------------------------
            await ImportStartPositionsAsync(document, newIndex.Id, cancellationToken);
            await UpdateProgressAsync(processing, 95, cancellationToken);

            // --- Regenerate previews on this machine --------------------------------------
            await EnqueuePreviewRenderAsync(newIndex.Id, cancellationToken);

            // Mark complete. ResultReference carries the new gallery index id so the caller can navigate to it.
            processing.Status = ProcessingStatus.Complete;
            processing.ProgressPercent = 100;
            processing.CompletedAt = DateTime.UtcNow;
            processing.ResultReference = newIndex.Id.ToString();
            await context.SaveChangesAsync(cancellationToken);

            logger.LogInformation(
                "Map import complete for ProcessingId {ProcessingId}. New CampaignMapGalleryIndex {IndexId} ('{MapName}').",
                processingId, newIndex.Id, newIndex.MapName);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Map import failed for ProcessingId {ProcessingId}.", processingId);

            processing.Status = ProcessingStatus.Failed;
            processing.CompletedAt = DateTime.UtcNow;
            processing.ErrorMessage = ex.Message;

            // Use a non-cancellable token so the failure is persisted even during shutdown.
            await context.SaveChangesAsync(CancellationToken.None);
        }
        finally
        {
            // The staged upload is no longer needed once the job has finished (success or failure).
            TryDeleteStagedZip(zipPath);
        }
    }

    /// <summary>
    /// Opens the staged zip and deserialises the <c>export.json</c> entry into a <see cref="MapExportDocument"/>.
    /// </summary>
    private static MapExportDocument ReadDocument(string zipPath)
    {
        using ZipArchive archive = ZipFile.OpenRead(zipPath);

        ZipArchiveEntry? entry = archive.GetEntry("export.json");
        if (entry is null)
        {
            throw new InvalidOperationException("The import zip does not contain an 'export.json' entry.");
        }

        using Stream entryStream = entry.Open();
        MapExportDocument? document = JsonSerializer.Deserialize<MapExportDocument>(entryStream, _jsonOptions);

        if (document is null)
        {
            throw new InvalidOperationException("The import zip's 'export.json' could not be parsed.");
        }

        return document;
    }

    /// <summary>
    /// Inserts any biomes referenced by the export that are not already present.
    /// </summary>
    /// <remarks>
    /// Existing biomes are left untouched — other maps depend on that shared reference data. Base resource
    /// values are exported as whole numbers and converted back to the scaled raw storage value here.
    /// </remarks>
    private async Task ImportMissingBiomesAsync(MapExportDocument document, CancellationToken cancellationToken)
    {
        if (document.Biomes.Count == 0)
        {
            return;
        }

        HashSet<BiomeType> existingBiomeIds = await context.CampaignBiomes
            .Select(b => b.BiomeTypeId)
            .ToHashSetAsync(cancellationToken);

        bool added = false;

        foreach (MapExportBiome exportBiome in document.Biomes)
        {
            if (existingBiomeIds.Contains(exportBiome.BiomeTypeId))
            {
                continue;
            }

            context.CampaignBiomes.Add(new CampaignBiome
            {
                BiomeTypeId = exportBiome.BiomeTypeId,
                Name = exportBiome.Name,
                MovementCost = exportBiome.MovementCost,
                DoesBlockSight = exportBiome.DoesBlockSight,
                IsWater = exportBiome.IsWater,
                IsTraversable = exportBiome.IsTraversable,
                RegenerationPercent = exportBiome.RegenerationPercent,
                IsPaintableOnCampaignMap = exportBiome.IsPaintableOnCampaignMap,
                MapEditorHtmlColor = exportBiome.MapEditorHtmlColor,
                CanBeElevated = exportBiome.CanBeElevated,
                // Base values are exported as whole numbers; convert back to the scaled raw storage value.
                BaseForageValue = ScaledResourceValue.ToRaw(exportBiome.BaseForageValue),
                BaseHuntValue = ScaledResourceValue.ToRaw(exportBiome.BaseHuntValue),
                BaseFishValue = ScaledResourceValue.ToRaw(exportBiome.BaseFishValue),
                BaseFodderValue = ScaledResourceValue.ToRaw(exportBiome.BaseFodderValue),
                BaseBoneValue = ScaledResourceValue.ToRaw(exportBiome.BaseBoneValue),
                BaseWoodValue = ScaledResourceValue.ToRaw(exportBiome.BaseWoodValue),
                BaseStoneValue = ScaledResourceValue.ToRaw(exportBiome.BaseStoneValue),
                BaseSpecialValue = ScaledResourceValue.ToRaw(exportBiome.BaseSpecialValue)
            });

            added = true;
        }

        if (added)
        {
            await context.SaveChangesAsync(cancellationToken);
        }
    }

    /// <summary>
    /// Inserts any geographies referenced by the export that are not already present. Existing rows are left untouched.
    /// </summary>
    private async Task ImportMissingGeographiesAsync(MapExportDocument document, CancellationToken cancellationToken)
    {
        if (document.Geographies.Count == 0)
        {
            return;
        }

        HashSet<CampaignMapGeographyType> existingGeographyIds = await context.CampaignGeographies
            .Select(g => g.CampaignMapGeographyTypeId)
            .ToHashSetAsync(cancellationToken);

        bool added = false;

        foreach (MapExportGeography exportGeography in document.Geographies)
        {
            if (existingGeographyIds.Contains(exportGeography.GeographyTypeId))
            {
                continue;
            }

            context.CampaignGeographies.Add(new CampaignGeography
            {
                CampaignMapGeographyTypeId = exportGeography.GeographyTypeId,
                Name = exportGeography.Name,
                RelativeElevation = exportGeography.RelativeElevation,
                MovementCost = exportGeography.MovementCost,
                MapEditorIcon = exportGeography.MapEditorIcon,
                IsPaintableOnCampaignMap = exportGeography.IsPaintableOnCampaignMap
            });

            added = true;
        }

        if (added)
        {
            await context.SaveChangesAsync(cancellationToken);
        }
    }

    /// <summary>
    /// Inserts a new <see cref="CampaignMapGalleryIndex"/> from the export, resolving a unique map name first.
    /// </summary>
    /// <remarks>The map already has its tiles, so it is recorded as <see cref="MapCreationStatus.Complete"/>.</remarks>
    private async Task<CampaignMapGalleryIndex> CreateIndexAsync(MapExportDocument document, CancellationToken cancellationToken)
    {
        MapExportIndex exportIndex = document.Map;

        // Import bypasses the create-map validator, so enforce name uniqueness here.
        List<string> existingNames = await context.CampaignMapGalleryIndex
            .Select(x => x.MapName)
            .ToListAsync(cancellationToken);

        string uniqueName = ResolveUniqueMapName(exportIndex.MapName, existingNames);

        var newIndex = new CampaignMapGalleryIndex
        {
            MapName = uniqueName,
            Description = exportIndex.Description,
            Width = exportIndex.Width,
            Height = exportIndex.Height,
            CreationEngine = exportIndex.CreationEngine,
            CreationStatus = MapCreationStatus.Complete,
            Status = "Creation complete",
            ErrorMessage = null,
            PropertiesJson = exportIndex.PropertiesJson,
            HorizontalWrap = exportIndex.HorizontalWrap,
            VerticalWrap = exportIndex.VerticalWrap,
            // CampaignMapOffsetId is the first tile's global id; set once the tiles are allocated below.
            CampaignMapOffsetId = -1
        };

        context.CampaignMapGalleryIndex.Add(newIndex);
        await context.SaveChangesAsync(cancellationToken);

        return newIndex;
    }

    /// <summary>
    /// Inserts all tiles for the imported map, allocating globally-unique <c>CampaignMapGalleryId</c>s and
    /// reconstructing X/Y from each tile's row-major <c>RelativeId</c>.
    /// </summary>
    /// <remarks>
    /// <see cref="CampaignMapGallery.CampaignMapGalleryId"/> is not database-generated and is unique across
    /// every map, so ids are allocated as <c>MAX(CampaignMapGalleryId)+1</c>. The read and inserts run inside a
    /// transaction; background jobs are processed one at a time, but this keeps the allocation atomic.
    /// </remarks>
    private async Task ImportTilesAsync(MapExportDocument document, CampaignMapGalleryIndex newIndex, CancellationToken cancellationToken)
    {
        int width = newIndex.Width;

        await using IDbContextTransaction transaction = await context.Database.BeginTransactionAsync(cancellationToken);

        int maxExistingId = await context.CampaignMapGallery
            .Select(t => (int?)t.CampaignMapGalleryId)
            .MaxAsync(cancellationToken) ?? 0;

        int startId = maxExistingId + 1;
        int offset = 0;

        // Insert in RelativeId order so the allocated ids run in the same row-major order the generator uses.
        foreach (MapExportTile exportTile in document.Tiles.OrderBy(t => t.RelativeId))
        {
            context.CampaignMapGallery.Add(new CampaignMapGallery
            {
                CampaignMapGalleryId = startId + offset,
                CampaignMapGalleryIndexId = newIndex.Id,
                XCoord = exportTile.RelativeId % width,
                YCoord = exportTile.RelativeId / width,
                BiomeTypeId = exportTile.BiomeTypeId,
                CampaignMapGeographyTypeId = exportTile.GeographyTypeId,
                // FK scalars above drive the relationships; the required navigations are not needed for insert.
                Biome = null!,
                Geography = null!,
                Index = null!
            });

            offset++;
        }

        await context.SaveChangesAsync(cancellationToken);

        // The offset id mirrors what GenerateCampaignMapExecutor records: the first tile's global id.
        newIndex.CampaignMapOffsetId = startId;
        await context.SaveChangesAsync(cancellationToken);

        await transaction.CommitAsync(cancellationToken);
    }

    /// <summary>
    /// Inserts the authored start positions for the imported map, tied to the new index.
    /// </summary>
    private async Task ImportStartPositionsAsync(MapExportDocument document, int newIndexId, CancellationToken cancellationToken)
    {
        if (document.StartPositions.Count == 0)
        {
            return;
        }

        foreach (MapExportStartPosition exportPosition in document.StartPositions)
        {
            context.CampaignMapStartPositions.Add(new CampaignMapStartPosition
            {
                CampaignMapGalleryIndexId = newIndexId,
                XCoord = exportPosition.XCoord,
                YCoord = exportPosition.YCoord,
                DisplayName = exportPosition.DisplayName,
                LocationScore = exportPosition.LocationScore
            });
        }

        await context.SaveChangesAsync(cancellationToken);
    }

    /// <summary>
    /// Enqueues a preview-render job for the newly imported map so the preview PNGs are regenerated locally.
    /// </summary>
    private async Task EnqueuePreviewRenderAsync(int newIndexId, CancellationToken cancellationToken)
    {
        // Metadata key matches the convention read by RenderCampaignMapPreviewExecutor.
        string metadataJson = JsonSerializer.Serialize(new { campaignMapGalleryIndexId = newIndexId });

        var renderProcessing = new Processing
        {
            ProcessType = ProcessType.RenderCampaignMapPreview,
            Status = ProcessingStatus.Pending,
            StartedAt = DateTime.UtcNow,
            MetadataJson = metadataJson
        };

        context.Processing.Add(renderProcessing);
        await context.SaveChangesAsync(cancellationToken);

        await processingChannel.Writer.WriteAsync(renderProcessing.ProcessingId, cancellationToken);

        logger.LogInformation(
            "Preview render job enqueued after import. ProcessingId: {ProcessingId}, CampaignMapGalleryIndexId: {IndexId}",
            renderProcessing.ProcessingId, newIndexId);
    }

    /// <summary>
    /// Resolves a map name that is unique among <paramref name="existingNames"/>, appending <c>" (N)"</c>
    /// (N from 2) on collision and keeping the result within the column length limit.
    /// </summary>
    private static string ResolveUniqueMapName(string desiredName, IEnumerable<string> existingNames)
    {
        const int maxLength = CampaignMapGalleryIndex.MAX_MAP_NAME_LENGTH;

        // Map-name comparison is case-insensitive to match the database's default collation.
        var taken = new HashSet<string>(existingNames, StringComparer.OrdinalIgnoreCase);

        string baseName = desiredName.Length > maxLength ? desiredName[..maxLength] : desiredName;
        if (!taken.Contains(baseName))
        {
            return baseName;
        }

        for (int n = 2; ; n++)
        {
            string suffix = $" ({n})";

            // Truncate the base name so the candidate (base + suffix) still fits within the column limit.
            string trimmedBase = desiredName.Length + suffix.Length > maxLength
                ? desiredName[..(maxLength - suffix.Length)]
                : desiredName;

            string candidate = trimmedBase + suffix;
            if (!taken.Contains(candidate))
            {
                return candidate;
            }
        }
    }

    /// <summary>
    /// Deletes the staged upload, swallowing and logging any failure (cleanup must never fail the job).
    /// </summary>
    private void TryDeleteStagedZip(string zipPath)
    {
        if (string.IsNullOrWhiteSpace(zipPath))
        {
            return;
        }

        try
        {
            if (File.Exists(zipPath))
            {
                File.Delete(zipPath);
            }
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Failed to delete staged import zip {ZipPath}.", zipPath);
        }
    }

    /// <summary>
    /// Persists a progress percentage update to the Processing record.
    /// </summary>
    private async Task UpdateProgressAsync(Processing processing, byte percent, CancellationToken cancellationToken)
    {
        processing.ProgressPercent = percent;
        await context.SaveChangesAsync(cancellationToken);
    }

    /// <summary>
    /// Reads the <c>importZipPath</c> value from the Processing record's MetadataJson.
    /// </summary>
    private static string ExtractImportZipPath(string? metadataJson)
    {
        if (string.IsNullOrWhiteSpace(metadataJson))
        {
            throw new InvalidOperationException("Processing record is missing MetadataJson.");
        }

        using JsonDocument doc = JsonDocument.Parse(metadataJson);
        if (!doc.RootElement.TryGetProperty("importZipPath", out JsonElement prop) ||
            prop.GetString() is not { Length: > 0 } path)
        {
            throw new InvalidOperationException("MetadataJson does not contain a valid 'importZipPath' property.");
        }

        return path;
    }
}
