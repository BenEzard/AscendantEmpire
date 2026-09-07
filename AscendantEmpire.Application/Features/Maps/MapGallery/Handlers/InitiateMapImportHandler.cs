using AscendantEmpire.Application.BackgroundServices;
using AscendantEmpire.Application.Common.Messaging;
using AscendantEmpire.Application.Data;
using AscendantEmpire.Domain.Entities.BackgroundProcessing;
using AscendantEmpire.Domain.Enums.Processing;
using AscendantEmpire.Domain.Enums.System;

using Microsoft.Extensions.Logging;

using System.Text.Json;

using static AscendantEmpire.Application.Common.ServiceResultBase;

namespace AscendantEmpire.Application.Features.Maps.MapGallery.Handlers;

/// <summary>
/// Creates a <see cref="Processing"/> record for an uploaded import zip and enqueues the job
/// for background processing.
/// </summary>
/// <remarks>
/// The controller has already staged the uploaded zip to disk; <paramref name="ZipPath"/> points at it.
/// Returns the new <c>ProcessingId</c> so the caller can poll for status.
/// </remarks>
public record InitiateMapImportRequest(string ZipPath) : IRequest<ServiceResult<int>>;

internal class InitiateMapImportHandler(
    ILogger<InitiateMapImportHandler> logger,
    AscendantEmpireContext context,
    IBackgroundProcessingChannel processingChannel
) : IRequestHandler<InitiateMapImportRequest, ServiceResult<int>>
{
    public async Task<ServiceResult<int>> Handle(
        InitiateMapImportRequest request,
        CancellationToken cancellationToken)
    {
        try
        {
            // The controller stages the upload before sending this request; bail out if it is missing.
            if (string.IsNullOrWhiteSpace(request.ZipPath) || !File.Exists(request.ZipPath))
            {
                const string missingMsg = "The uploaded import file could not be found on the server.";
                logger.LogWarning("{Error} Path: {ZipPath}", missingMsg, request.ZipPath);
                return ServiceResult<int>.Fail(missingMsg);
            }

            // Persist a Pending record so the caller immediately has a processId to poll.
            string metadataJson = JsonSerializer.Serialize(new { importZipPath = request.ZipPath });

            var processing = new Processing
            {
                ProcessType = ProcessType.ImportGalleryMap,
                Status = ProcessingStatus.Pending,
                // StartedAt will be overwritten by the background service when processing begins; set it now so the column satisfies the non-null constraint.
                StartedAt = DateTime.UtcNow,
                MetadataJson = metadataJson
            };

            context.Processing.Add(processing);
            await context.SaveChangesAsync(cancellationToken);

            // Enqueue the job. The background service will update the record to InProgress.
            await processingChannel.Writer.WriteAsync(processing.ProcessingId, cancellationToken);

            logger.LogInformation("Map import job enqueued. ProcessingId: {ProcessingId}, ZipPath: {ZipPath}", processing.ProcessingId, request.ZipPath);

            return ServiceResult<int>.Created(processing.ProcessingId);
        }
        catch (Exception ex)
        {
            const string error = "An error occurred while initiating the map import.";
            logger.LogError(ex, error + " ZipPath: {ZipPath}", request.ZipPath);
            return ServiceResult<int>.InternalServerError(error);
        }
    }
}
