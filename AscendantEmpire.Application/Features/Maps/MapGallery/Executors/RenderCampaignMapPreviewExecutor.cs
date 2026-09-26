using System.Text.Json;

using AscendantEmpire.Application.BackgroundServices;
using AscendantEmpire.Application.Data;
using AscendantEmpire.Application.MapGeneration;
using AscendantEmpire.Application.Options;
using AscendantEmpire.Domain.Entities.BackgroundProcessing;
using AscendantEmpire.Domain.Entities.Maps.CampaignMaps;
using AscendantEmpire.Domain.Enums.Maps;
using AscendantEmpire.Domain.Enums.Processing;
using AscendantEmpire.Domain.Enums.System;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace AscendantEmpire.Application.Features.Maps.MapGallery.Executors;

/// <summary>
/// Executes <see cref="ProcessType.RenderCampaignMapPreview"/> jobs.
/// </summary>
/// <remarks>
/// Reloads a campaign map's tiles from the database (using only the index id carried in the job's <c>MetadataJson</c>),
/// rebuilds the geography/biome arrays, renders the preview PNGs via <see cref="MapPreviewRenderer"/>, and writes the
/// resulting file paths back onto the <see cref="CampaignMapGalleryIndex"/>.
/// </remarks>
internal sealed class RenderCampaignMapPreviewExecutor(
    AscendantEmpireContext context,
    IOptions<MapGenerationSettings> mapGenOptions,
    ILogger<RenderCampaignMapPreviewExecutor> logger
) : IProcessExecutor
{
    public ProcessType ProcessType => ProcessType.RenderCampaignMapPreview;

    public async Task ExecuteAsync(Processing processing, CancellationToken cancellationToken)
    {
        int processingId = processing.ProcessingId;
        logger.LogInformation("Starting map preview render for ProcessingId {ProcessingId}.", processingId);

        // Mark as in-progress so the UI can see it has started.
        processing.Status = ProcessingStatus.InProgress;
        processing.StartedAt = DateTime.UtcNow;
        processing.ProgressPercent = 5;
        await context.SaveChangesAsync(cancellationToken);

        try
        {
            int indexId = ExtractCampaignMapGalleryIndexId(processing.MetadataJson);

            // Load the index tracked — we write the rendered paths back onto it below.
            CampaignMapGalleryIndex? index = await context.CampaignMapGalleryIndex.FirstOrDefaultAsync(x => x.Id == indexId, cancellationToken);

            if (index is null)
            {
                throw new InvalidOperationException($"CampaignMapGalleryIndex {indexId} not found.");
            }

            int width = index.Width;
            int height = index.Height;

            // Load all tiles for this map.
            List<CampaignMapGallery> tiles = await context.CampaignMapGallery
                .AsNoTracking()
                .Where(t => t.CampaignMapGalleryIndexId == indexId)
                .ToListAsync(cancellationToken);

            processing.ProgressPercent = 50;
            await context.SaveChangesAsync(cancellationToken);

            // Rebuild the row-major (y * width + x) arrays the renderer expects. The two river arrays are the South and East edges
            // each tile owns, which between them are every edge on the map exactly once.
            var geography = new CampaignMapGeographyType[width * height];
            var biomes = new BiomeType[width * height];
            var southRivers = new RiverWidth[width * height];
            var eastRivers = new RiverWidth[width * height];

            foreach (CampaignMapGallery tile in tiles)
            {
                int idx = tile.YCoord * width + tile.XCoord;
                geography[idx] = tile.CampaignMapGeographyTypeId;
                biomes[idx] = tile.BiomeTypeId;
                southRivers[idx] = tile.SouthRiver;
                eastRivers[idx] = tile.EastRiver;
            }

            // Render the previews. MapPreviewRenderer is best-effort: on failure it returns (null, null),
            // which simply clears the stored paths rather than failing the job.
            (index.GeographyMapPath, index.BiomeMapPath) = MapPreviewRenderer.Render(
                geography,
                biomes,
                width,
                height,
                mapGenOptions.Value.OutputDirectory,
                index.Id,
                southRivers,
                eastRivers);

            // Mark complete.
            processing.Status = ProcessingStatus.Complete;
            processing.ProgressPercent = 100;
            processing.CompletedAt = DateTime.UtcNow;
            await context.SaveChangesAsync(cancellationToken);

            logger.LogInformation("Map preview render complete for CampaignMapGalleryIndex {IndexId} (ProcessingId {ProcessingId}).", indexId, processingId);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Map preview render failed for ProcessingId {ProcessingId}.", processingId);

            processing.Status = ProcessingStatus.Failed;
            processing.CompletedAt = DateTime.UtcNow;
            processing.ErrorMessage = ex.Message;

            // Use a non-cancellable token so the failure is persisted even during shutdown.
            await context.SaveChangesAsync(CancellationToken.None);
        }
    }

    /// <summary>
    /// Reads the <c>campaignMapGalleryIndexId</c> value from the Processing record's MetadataJson.
    /// </summary>
    private static int ExtractCampaignMapGalleryIndexId(string? metadataJson)
    {
        if (string.IsNullOrWhiteSpace(metadataJson))
        {
            throw new InvalidOperationException("Processing record is missing MetadataJson.");
        }

        using JsonDocument doc = JsonDocument.Parse(metadataJson);
        if (!doc.RootElement.TryGetProperty("campaignMapGalleryIndexId", out JsonElement prop) ||
            !prop.TryGetInt32(out int id))
        {
            throw new InvalidOperationException(
                "MetadataJson does not contain a valid 'campaignMapGalleryIndexId' property.");
        }

        return id;
    }
}
