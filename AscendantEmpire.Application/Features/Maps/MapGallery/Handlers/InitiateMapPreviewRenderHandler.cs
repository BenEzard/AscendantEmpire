using AscendantEmpire.Application.BackgroundServices;
using AscendantEmpire.Application.Data;
using AscendantEmpire.Domain.Entities.BackgroundProcessing;
using AscendantEmpire.Domain.Enums.Processing;
using AscendantEmpire.Domain.Enums.System;

using MediatR;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

using System.Text.Json;

using static AscendantEmpire.Application.Common.ServiceResultBase;

namespace AscendantEmpire.Application.Features.Maps.MapGallery.Handlers;

/// <summary>
/// Creates a <see cref="Processing"/> record and enqueues a preview-render job for background processing.
/// </summary>
/// <remarks>
/// Returns the new <c>ProcessingId</c> so the caller can poll for status. This is the single, reusable entry
/// point for triggering a preview re-render — both map generation and (later) map editing enqueue through it.
/// </remarks>
public record InitiateMapPreviewRenderRequest(int CampaignMapGalleryIndexId) : IRequest<ServiceResult<int>>;

internal class InitiateMapPreviewRenderHandler(
    ILogger<InitiateMapPreviewRenderHandler> logger,
    AscendantEmpireContext context,
    IBackgroundProcessingChannel processingChannel
) : IRequestHandler<InitiateMapPreviewRenderRequest, ServiceResult<int>>
{
    public async Task<ServiceResult<int>> Handle(
        InitiateMapPreviewRenderRequest request,
        CancellationToken cancellationToken)
    {
        try
        {
            // Verify the map exists before accepting the job.
            bool mapExists = await context.CampaignMapGalleryIndex
                .AnyAsync(x => x.Id == request.CampaignMapGalleryIndexId, cancellationToken);

            if (!mapExists)
            {
                string notFoundMsg = $"Campaign Map Gallery Index {request.CampaignMapGalleryIndexId} not found.";
                logger.LogWarning("{Error}", notFoundMsg);
                return ServiceResult<int>.NotFound(notFoundMsg);
            }

            // Persist a Pending record so the caller immediately has a processId to poll.
            // The metadata key matches the convention read by the campaign-map executors.
            string metadataJson = JsonSerializer.Serialize(new { campaignMapGalleryIndexId = request.CampaignMapGalleryIndexId });

            var processing = new Processing
            {
                ProcessType = ProcessType.RenderCampaignMapPreview,
                Status = ProcessingStatus.Pending,
                // StartedAt will be overwritten by the background service when processing begins; set it now so the column satisfies the non-null constraint.
                StartedAt = DateTime.UtcNow,
                MetadataJson = metadataJson
            };

            context.Processing.Add(processing);
            await context.SaveChangesAsync(cancellationToken);

            // Enqueue the job. The background service will update the record to InProgress.
            await processingChannel.Writer.WriteAsync(processing.ProcessingId, cancellationToken);

            logger.LogInformation("Map preview render job enqueued. ProcessingId: {ProcessingId}, CampaignMapGalleryIndexId: {IndexId}", processing.ProcessingId, request.CampaignMapGalleryIndexId);

            return ServiceResult<int>.Created(processing.ProcessingId);
        }
        catch (Exception ex)
        {
            const string error = "An error occurred while initiating the map preview render.";
            logger.LogError(ex, error + " CampaignMapGalleryIndexId: {IndexId}", request.CampaignMapGalleryIndexId);
            return ServiceResult<int>.InternalServerError(error);
        }
    }
}
