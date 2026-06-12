using System.Text.Json;

using AscendantEmpire.Application.BackgroundServices;
using AscendantEmpire.Application.Common;
using AscendantEmpire.Application.Data;
using AscendantEmpire.Application.Features.Maps.MapGallery.Handlers;
using AscendantEmpire.Application.Options;
using AscendantEmpire.Domain.Entities.BackgroundProcessing;
using AscendantEmpire.Domain.Entities.Maps.CampaignMaps;
using AscendantEmpire.Domain.Enums.Maps;
using AscendantEmpire.Domain.Enums.Processing;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

using static AscendantEmpire.Application.Common.ServiceResultBase;

namespace AscendantEmpire.Application.Features.Maps.MapGallery.Executors;

/// <summary>
/// Executes <see cref="ProcessType.GenerateCampaignMap"/> jobs.
/// </summary>
/// <remarks>
/// Generates the tiles for a single queued <see cref="CampaignMapGalleryIndex"/> (the one referenced by the job's <c>MetadataJson</c>) using the engine recorded on the index, 
/// and reports progress on both the <see cref="Processing"/> record and the index itself.
/// </remarks>
internal sealed class GenerateCampaignMapExecutor(
    AscendantEmpireContext context,
    IServiceScopeFactory scopeFactory,
    IOptions<MapGenerationSettings> mapGenOptions,
    ILogger<GenerateCampaignMapExecutor> logger
) : IProcessExecutor
{
    public ProcessType ProcessType => ProcessType.GenerateCampaignMap;

    public async Task ExecuteAsync(Processing processing, CancellationToken cancellationToken)
    {
        int processingId = processing.ProcessingId;
        int indexId = ExtractCampaignMapGalleryIndexId(processing.MetadataJson);

        logger.LogInformation(
            "Starting campaign map generation for CampaignMapGalleryIndex {IndexId} (ProcessingId {ProcessingId}).",
            indexId, processingId);

        CampaignMapGalleryIndex? index = await context.CampaignMapGalleryIndex
            .FirstOrDefaultAsync(x => x.Id == indexId, cancellationToken);

        if (index is null)
        {
            throw new InvalidOperationException($"CampaignMapGalleryIndex {indexId} not found.");
        }

        // Mark both the Processing record and the index as started.
        processing.Status = ProcessingStatus.InProgress;
        processing.StartedAt = DateTime.UtcNow;
        index.CreationStatus = MapCreationStatus.Creating;
        index.Status = "Creation in progress";
        await SetProgressAsync(processing, index, 5, cancellationToken);

        // Refresh the queue position labels of any other maps still waiting.
        await UpdatePendingQueueLabelsAsync(indexId, cancellationToken);

        try
        {
            // Pick the generator that matches the engine recorded on the index.
            ICampaignMapGenerator generator = index.CreationEngine switch
            {
                MapCreationEngine.BlankMap => new GenerateBlankCampaignMap(scopeFactory),
                MapCreationEngine.PerlinNoiseV1 => new GeneratePerlinNoiseCampaignMap(scopeFactory, mapGenOptions),
                _ => throw new InvalidOperationException($"Unsupported creation engine '{index.CreationEngine}'.")
            };

            // --- Validate -------------------------------------------------------
            ServiceResultBase validationResult = generator.ValidateRequestProperties(index);

            if (!validationResult.HasSucceeded)
            {
                await FailAsync(processing, index, "Error during validation", validationResult.Errors, cancellationToken);
                return;
            }

            await SetProgressAsync(processing, index, 20, cancellationToken);

            // --- Generate -------------------------------------------------------
            await SetProgressAsync(processing, index, 50, cancellationToken);

            ServiceResult<int> createResult = await generator.GenerateCampaignMapAsync(index);

            if (!createResult.HasSucceeded)
            {
                await FailAsync(processing, index, "Error during creation", createResult.Errors, cancellationToken);
                return;
            }

            await SetProgressAsync(processing, index, 90, cancellationToken);

            // --- Complete -------------------------------------------------------
            index.CreationStatus = MapCreationStatus.Complete;
            index.Status = "Creation complete";
            index.ErrorMessage = null;
            index.CampaignMapOffsetId = createResult.SuccessValue;

            processing.Status = ProcessingStatus.Complete;
            processing.CompletedAt = DateTime.UtcNow;
            await SetProgressAsync(processing, index, 100, cancellationToken);

            logger.LogInformation(
                "Campaign map generation complete for CampaignMapGalleryIndex {IndexId} (ProcessingId {ProcessingId}).",
                indexId, processingId);
        }
        catch (Exception ex)
        {
            logger.LogError(ex,
                "Campaign map generation failed for CampaignMapGalleryIndex {IndexId} (ProcessingId {ProcessingId}).",
                indexId, processingId);

            await FailAsync(processing, index, "Error during creation", [ex.Message], cancellationToken);
        }
    }

    /// <summary>
    /// Records a failure on both the Processing record and the index, then saves.
    /// </summary>
    /// <remarks>
    /// Uses a non-cancellable token so the failure is persisted even during shutdown.
    /// </remarks>
    private async Task FailAsync(
        Processing processing,
        CampaignMapGalleryIndex index,
        string statusMessage,
        IReadOnlyCollection<string> errors,
        CancellationToken cancellationToken)
    {
        string errorMessage = string.Join(Environment.NewLine, errors);

        index.CreationStatus = MapCreationStatus.InError;
        index.Status = statusMessage;
        index.ErrorMessage = errorMessage;

        processing.Status = ProcessingStatus.Failed;
        processing.CompletedAt = DateTime.UtcNow;
        processing.ErrorMessage = errorMessage;

        // cancellationToken is accepted for symmetry but we always persist failures.
        _ = cancellationToken;
        await context.SaveChangesAsync(CancellationToken.None);
    }

    /// <summary>
    /// Sets the same progress percentage on both the Processing record and the index, then saves.
    /// </summary>
    private async Task SetProgressAsync(
        Processing processing,
        CampaignMapGalleryIndex index,
        byte percent,
        CancellationToken cancellationToken)
    {
        processing.ProgressPercent = percent;
        index.ProgressPercent = percent;
        await context.SaveChangesAsync(cancellationToken);
    }

    /// <summary>
    /// Re-numbers the "#N in queue" status labels for maps still pending creation, excluding the one currently being generated.
    /// </summary>
    private async Task UpdatePendingQueueLabelsAsync(int currentIndexId, CancellationToken cancellationToken)
    {
        List<CampaignMapGalleryIndex> pendingMaps = await context.CampaignMapGalleryIndex
            .Where(cmi => cmi.CreationStatus == MapCreationStatus.Pending && cmi.Id != currentIndexId)
            .OrderBy(cmi => cmi.Id)
            .ToListAsync(cancellationToken);

        int position = 1;
        foreach (CampaignMapGalleryIndex pendingMap in pendingMaps)
        {
            pendingMap.Status = $"Pending creation (#{position} in queue)";
            position++;
        }

        await context.SaveChangesAsync(cancellationToken);
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
