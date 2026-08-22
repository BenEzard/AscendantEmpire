using AscendantEmpire.Application.BackgroundServices;
using AscendantEmpire.Application.Data;
using AscendantEmpire.Domain.Entities.BackgroundProcessing;
using AscendantEmpire.Domain.Enums.Processing;
using AscendantEmpire.Domain.Enums.System;

using MediatR;

using Microsoft.Extensions.Logging;

using System.Text.Json;

using static AscendantEmpire.Application.Common.ServiceResultBase;

namespace AscendantEmpire.Application.Features.UnitComponents.Handlers;

/// <summary>
/// Creates a <see cref="Processing"/> record for a catalogue export and enqueues it for background processing.
/// </summary>
/// <remarks>
/// Returns the new <c>ProcessingId</c> so the caller can poll for status and then download the zip.
/// </remarks>
public record InitiateCatalogueExportRequest(bool ExportComponents, bool ExportTemplates) : IRequest<ServiceResult<int>>;

internal class InitiateCatalogueExportHandler(
    ILogger<InitiateCatalogueExportHandler> logger,
    AscendantEmpireContext context,
    IBackgroundProcessingChannel processingChannel
) : IRequestHandler<InitiateCatalogueExportRequest, ServiceResult<int>>
{
    public async Task<ServiceResult<int>> Handle(InitiateCatalogueExportRequest request, CancellationToken cancellationToken)
    {
        try
        {
            // Nothing selected means nothing to export — reject before staging a job.
            if (!request.ExportComponents && !request.ExportTemplates)
            {
                return ServiceResult<int>.Fail("Select at least one catalogue kind to export.");
            }

            // Persist a Pending record so the caller immediately has a processId to poll. The executor reads
            // the selection flags back from MetadataJson.
            string metadataJson = JsonSerializer.Serialize(new
            {
                exportComponents = request.ExportComponents,
                exportTemplates = request.ExportTemplates
            });

            var processing = new Processing
            {
                ProcessType = ProcessType.ExportCatalogue,
                Status = ProcessingStatus.Pending,
                // StartedAt is overwritten when processing begins; set now so the column satisfies its non-null constraint.
                StartedAt = DateTime.UtcNow,
                MetadataJson = metadataJson
            };

            context.Processing.Add(processing);
            await context.SaveChangesAsync(cancellationToken);

            await processingChannel.Writer.WriteAsync(processing.ProcessingId, cancellationToken);

            logger.LogInformation(
                "Catalogue export job enqueued. ProcessingId: {ProcessingId}, Components: {Components}, Templates: {Templates}",
                processing.ProcessingId, request.ExportComponents, request.ExportTemplates);

            return ServiceResult<int>.Created(processing.ProcessingId);
        }
        catch (Exception ex)
        {
            const string error = "An error occurred while initiating the catalogue export.";
            logger.LogError(ex, error);
            return ServiceResult<int>.InternalServerError(error);
        }
    }
}
