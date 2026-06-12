using AscendantEmpire.Application.BackgroundServices;
using AscendantEmpire.Application.Data;
using AscendantEmpire.Domain.Entities.BackgroundProcessing;
using AscendantEmpire.Domain.Entities.Maps.CampaignMaps;
using AscendantEmpire.Domain.Enums.Maps;
using AscendantEmpire.Domain.Enums.Processing;
using AscendantEmpire.FrontendContracts.MapGallery;

using MediatR;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

using System.Text.Json;

using static AscendantEmpire.Application.Common.ServiceResultBase;

namespace AscendantEmpire.Application.Features.Maps.MapGallery.Handlers;

public record AddCampaignMapRequest(
    string MapName,
    string Description,
    int Width,
    int Height,
    MapCreationEngine CreationEngine,
    Dictionary<string, string>? Parameters
) : IRequest<ServiceResult<AddCampaignMapResponse>>, ICampaignMapIndexUpsertRequest
{
    public int? Id => null; // satisfies the interface
}

/// <summary>
/// Returned to the caller after a map is queued.
/// </summary>
/// <remarks>
/// <see cref="ProcessingId"/> can be polled via <c>GET /system/status?processId=X</c> to drive the generation progress UI.
/// </remarks>
public record AddCampaignMapResponse(int GalleryIndexId, int ProcessingId);

internal class AddCampaignMapHandler(
    ILogger<AddCampaignMapHandler> logger,
    IDbContextFactory<AscendantEmpireContext> dbFactory,
    IBackgroundProcessingChannel processingChannel
    ) : IRequestHandler<AddCampaignMapRequest, ServiceResult<AddCampaignMapResponse>>
{
    public async Task<ServiceResult<AddCampaignMapResponse>> Handle(AddCampaignMapRequest request, CancellationToken cancellationToken)
    {
        try
        {
            await using var context = await dbFactory.CreateDbContextAsync(cancellationToken);

            // Normalize inputs
            string mapName = request.MapName.Trim();
            string description = request.Description?.Trim() ?? string.Empty;
            string propsJson = request.Parameters != null
                ? JsonSerializer.Serialize(request.Parameters)
                : string.Empty;

            int pendingMapsCount = await context.CampaignMapGalleryIndex.CountAsync(x => x.CreationStatus == MapCreationStatus.Pending, cancellationToken);

            var newIndexRecord = new CampaignMapGalleryIndex
            {
                MapName = mapName,
                Description = description,
                Width = request.Width,
                Height = request.Height,
                CreationStatus = MapCreationStatus.Pending,
                CreationEngine = request.CreationEngine,
                Status = $"Pending creation (#{pendingMapsCount + 1} in queue)",
                ErrorMessage = string.Empty,
                PropertiesJson = propsJson,
                CampaignMapOffsetId = -1, // default until creation completes
                HorizontalWrap = request.Parameters != null
                    && request.Parameters.TryGetValue("HorizontalWrap", out var hWrap)
                    && bool.TryParse(hWrap, out var hWrapBool) ? hWrapBool : false,
                VerticalWrap = request.Parameters != null
                    && request.Parameters.TryGetValue("VerticalWrap", out var vWrap)
                    && bool.TryParse(vWrap, out var vWrapBool) ? vWrapBool : false,
            };

            context.CampaignMapGalleryIndex.Add(newIndexRecord);
            await context.SaveChangesAsync(cancellationToken);

            // Queue the generation job. A Processing record gives the caller a processId to poll,
            // and writing its id to the channel hands the job to the BackgroundProcessingService.
            string metadataJson = JsonSerializer.Serialize(new { campaignMapGalleryIndexId = newIndexRecord.Id });

            var processing = new Processing
            {
                ProcessType = ProcessType.GenerateCampaignMap,
                Status = ProcessingStatus.Pending,
                // StartedAt is overwritten when the background service begins; set now to satisfy the non-null column.
                StartedAt = DateTime.UtcNow,
                MetadataJson = metadataJson
            };

            context.Processing.Add(processing);
            await context.SaveChangesAsync(cancellationToken);

            await processingChannel.Writer.WriteAsync(processing.ProcessingId, cancellationToken);

            logger.LogInformation(
                "Campaign map generation queued. CampaignMapGalleryIndexId: {IndexId}, ProcessingId: {ProcessingId}",
                newIndexRecord.Id, processing.ProcessingId);

            return ServiceResult<AddCampaignMapResponse>.Created(
                new AddCampaignMapResponse(newIndexRecord.Id, processing.ProcessingId));
        }
        catch (Exception ex)
        {
            const string error = "Error creating Campaign Map Gallery Index record.";
            logger.LogError(ex, error);
            return ServiceResult<AddCampaignMapResponse>.InternalServerError(error);
        }
    }
}
