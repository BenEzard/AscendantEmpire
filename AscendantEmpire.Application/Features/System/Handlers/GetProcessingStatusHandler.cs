using AscendantEmpire.Application.Data;
using AscendantEmpire.Application.Features.SystemMonitoring.Classes;
using AscendantEmpire.Domain.Entities.BackgroundProcessing;

using MediatR;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

using static AscendantEmpire.Application.Common.ServiceResultBase;

namespace AscendantEmpire.Application.Features.SystemMonitoring.Handlers;

/// <summary>
/// Returns the current status of a background process by its <c>ProcessingId</c>.
/// The file path (<c>ResultReference</c>) is intentionally not exposed to API consumers.
/// </summary>
public record GetProcessingStatusRequest(int ProcessingId) : IRequest<ServiceResult<DTProcessingStatus>>;

internal class GetProcessingStatusHandler(
    ILogger<GetProcessingStatusHandler> logger,
    AscendantEmpireContext context
) : IRequestHandler<GetProcessingStatusRequest, ServiceResult<DTProcessingStatus>>
{
    public async Task<ServiceResult<DTProcessingStatus>> Handle(
        GetProcessingStatusRequest request,
        CancellationToken cancellationToken)
    {
        try
        {
            Processing? processing = await context.Processing
                .AsNoTracking()
                .FirstOrDefaultAsync(x => x.ProcessingId == request.ProcessingId, cancellationToken);

            if (processing is null)
            {
                string notFound = $"Processing record {request.ProcessingId} not found.";
                logger.LogWarning("{Error}", notFound);
                return ServiceResult<DTProcessingStatus>.NotFound(notFound);
            }

            var dto = new DTProcessingStatus
            {
                ProcessingId = processing.ProcessingId,
                ProcessType = processing.ProcessType,
                Status = processing.Status,
                ProgressPercent = processing.ProgressPercent,
                StartedAt = processing.StartedAt,
                CompletedAt = processing.CompletedAt,
                ErrorMessage = processing.ErrorMessage
            };

            return ServiceResult<DTProcessingStatus>.Succeed(dto);
        }
        catch (Exception ex)
        {
            const string error = "An error occurred while retrieving the processing status.";
            logger.LogError(ex, error + " ProcessingId: {ProcessingId}", request.ProcessingId);
            return ServiceResult<DTProcessingStatus>.InternalServerError(error);
        }
    }
}
