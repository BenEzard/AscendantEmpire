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
/// Creates a <see cref="Processing"/> record for a catalogue import and enqueues it for background processing.
/// </summary>
/// <remarks>
/// The controller stages the uploaded zip to disk first and passes its path here. Returns the new
/// <c>ProcessingId</c> so the caller can poll for status.
/// </remarks>
public record InitiateCatalogueImportRequest(string ZipPath) : IRequest<ServiceResult<int>>;

internal class InitiateCatalogueImportHandler(
    ILogger<InitiateCatalogueImportHandler> logger,
    AscendantEmpireContext context,
    IBackgroundProcessingChannel processingChannel
) : IRequestHandler<InitiateCatalogueImportRequest, ServiceResult<int>>
{
    public async Task<ServiceResult<int>> Handle(InitiateCatalogueImportRequest request, CancellationToken cancellationToken)
    {
        try
        {
            // The executor reads the staged zip path back from MetadataJson.
            string metadataJson = JsonSerializer.Serialize(new { importZipPath = request.ZipPath });

            var processing = new Processing
            {
                ProcessType = ProcessType.ImportCatalogue,
                Status = ProcessingStatus.Pending,
                // StartedAt is overwritten when processing begins; set now so the column satisfies its non-null constraint.
                StartedAt = DateTime.UtcNow,
                MetadataJson = metadataJson
            };

            context.Processing.Add(processing);
            await context.SaveChangesAsync(cancellationToken);

            await processingChannel.Writer.WriteAsync(processing.ProcessingId, cancellationToken);

            logger.LogInformation("Catalogue import job enqueued. ProcessingId: {ProcessingId}", processing.ProcessingId);

            return ServiceResult<int>.Created(processing.ProcessingId);
        }
        catch (Exception ex)
        {
            const string error = "An error occurred while initiating the catalogue import.";
            logger.LogError(ex, error);
            return ServiceResult<int>.InternalServerError(error);
        }
    }
}
