using AscendantEmpire.Application.BackgroundServices;
using AscendantEmpire.Application.Data;
using AscendantEmpire.Domain.Entities.BackgroundProcessing;
using AscendantEmpire.Domain.Enums.Processing;

using MediatR;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

using System.Text.Json;

using static AscendantEmpire.Application.Common.ServiceResultBase;

namespace AscendantEmpire.Application.Features.Maps.MapGallery.Handlers;

/// <summary>
/// Creates a <see cref="Processing"/> record and enqueues the job for background processing.
/// </summary>
/// <remarks>
/// Returns the new <c>ProcessingId</c> so the caller can poll for status.
/// </remarks>
public record InitiateMapExportRequest(int MapGalleryIndexId) : IRequest<ServiceResult<int>>;

internal class InitiateMapExportHandler(
    ILogger<InitiateMapExportHandler> logger,
    AscendantEmpireContext context,
    IBackgroundProcessingChannel processingChannel
) : IRequestHandler<InitiateMapExportRequest, ServiceResult<int>>
{
    public async Task<ServiceResult<int>> Handle(
        InitiateMapExportRequest request,
        CancellationToken cancellationToken)
    {
        try
        {
            // Verify the map exists before accepting the job.
            bool mapExists = await context.CampaignMapGalleryIndex
                .AnyAsync(x => x.Id == request.MapGalleryIndexId, cancellationToken);

            if (!mapExists)
            {
                string notFoundMsg = $"Campaign Map Gallery Index {request.MapGalleryIndexId} not found.";
                logger.LogWarning("{Error}", notFoundMsg);
                return ServiceResult<int>.NotFound(notFoundMsg);
            }

            // Persist a Pending record so the caller immediately has a processId to poll.
            string metadataJson = JsonSerializer.Serialize(new { mapGalleryIndexId = request.MapGalleryIndexId });

            var processing = new Processing
            {
                ProcessType = ProcessType.ExportGalleryMap,
                Status = ProcessingStatus.Pending,
                // StartedAt will be overwritten by the background service when processing begins; set it now so the column satisfies the non-null constraint.
                StartedAt = DateTime.UtcNow,
                MetadataJson = metadataJson
            };

            context.Processing.Add(processing);
            await context.SaveChangesAsync(cancellationToken);

            // Enqueue the job. The background service will update the record to InProgress.
            await processingChannel.Writer.WriteAsync(processing.ProcessingId, cancellationToken);

            logger.LogInformation("Map export job enqueued. ProcessingId: {ProcessingId}, MapGalleryIndexId: {MapId}", processing.ProcessingId, request.MapGalleryIndexId);

            return ServiceResult<int>.Created(processing.ProcessingId);
        }
        catch (Exception ex)
        {
            const string error = "An error occurred while initiating the map export.";
            logger.LogError(ex, error + " MapGalleryIndexId: {MapId}", request.MapGalleryIndexId);
            return ServiceResult<int>.InternalServerError(error);
        }
    }
}
