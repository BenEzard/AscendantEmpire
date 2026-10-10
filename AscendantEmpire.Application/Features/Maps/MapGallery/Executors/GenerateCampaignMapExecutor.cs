using System.Text.Json;

using AscendantEmpire.Application.BackgroundServices;
using AscendantEmpire.Application.Common;
using AscendantEmpire.Application.Data;
using AscendantEmpire.Application.Features.Maps.MapGallery.Handlers;
using AscendantEmpire.Application.Features.Maps.MapResources.Services;
using AscendantEmpire.Domain.Entities.BackgroundProcessing;
using AscendantEmpire.Domain.Entities.Maps.CampaignMaps;
using AscendantEmpire.Domain.Enums.Maps;
using AscendantEmpire.Domain.Enums.Processing;
using AscendantEmpire.Domain.Enums.System;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

using static AscendantEmpire.Application.Common.ServiceResultBase;

namespace AscendantEmpire.Application.Features.Maps.MapGallery.Executors;

/// <summary>
/// Executes <see cref="ProcessType.GenerateCampaignMap"/> jobs.
/// </summary>
/// <remarks>
/// <para>
/// Generates the tiles for a single queued <see cref="CampaignMapGalleryIndex"/> (the one referenced by the job's <c>MetadataJson</c>) using the engine recorded on the index,
/// and reports progress on both the <see cref="Processing"/> record and the index itself.
/// </para>
/// <para>
/// Once the tiles exist, it places the MapResources their Biomes imply. That step is hooked here rather than inside each generator so every engine gets it
/// from one call site, and because the generators are constructed by the switch below rather than resolved from the container.
/// </para>
/// </remarks>
internal sealed class GenerateCampaignMapExecutor(
    AscendantEmpireContext context,
    IServiceScopeFactory scopeFactory,
    IBiomeImpliedMapResourceService impliedMapResources,
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
                MapCreationEngine.PerlinNoiseV1 => new GeneratePerlinNoiseCampaignMap(scopeFactory),
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

            // --- Biome-implied MapResources -------------------------------------
            await PlaceBiomeImpliedMapResourcesAsync(index, cancellationToken);

            await SetProgressAsync(processing, index, 95, cancellationToken);

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
    /// Places the MapResources the generated tiles' Biomes imply, without ever failing the map.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Advisory by design. The tiles are written and correct by this point, and an author can place what is missing from the editor's MapResources palette,
    /// so a map whose Timber did not land is bare rather than wrong. Marking it <see cref="MapCreationStatus.InError"/> would strand a complete map: nothing
    /// re-runs the placement pass for a map that already has its tiles.
    /// </para>
    /// <para>
    /// The service returns its failures rather than throwing, so the catch here is for the unexpected. It has to be caught: the caller's catch calls
    /// <see cref="FailAsync"/>, which would put a map whose tiles all landed into error.
    /// </para>
    /// </remarks>
    private async Task PlaceBiomeImpliedMapResourcesAsync(CampaignMapGalleryIndex index, CancellationToken cancellationToken)
    {
        try
        {
            ServiceResult<int> placementResult = await impliedMapResources.PlaceForGeneratedMapAsync(index.Id, cancellationToken);

            if (placementResult.HasSucceeded)
            {
                logger.LogInformation(
                    "Placed {PlacementCount} Biome-implied MapResources on CampaignMapGalleryIndex {IndexId}.",
                    placementResult.SuccessValue, index.Id);

                return;
            }

            logger.LogWarning(
                "Biome-implied MapResources were not placed on CampaignMapGalleryIndex {IndexId}, which is complete without them: {Errors}",
                index.Id, string.Join(Environment.NewLine, placementResult.Errors));
        }
        catch (Exception ex)
        {
            logger.LogError(ex,
                "Placing Biome-implied MapResources threw for CampaignMapGalleryIndex {IndexId}. The map is complete without them.",
                index.Id);
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
