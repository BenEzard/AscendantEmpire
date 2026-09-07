using AscendantEmpire.Application.BackgroundServices;
using AscendantEmpire.Application.Common.Messaging;
using AscendantEmpire.Application.Data;
using AscendantEmpire.Domain.Entities.BackgroundProcessing;
using AscendantEmpire.Domain.Enums.Processing;
using AscendantEmpire.Domain.Enums.System;

using Microsoft.Extensions.Logging;

using static AscendantEmpire.Application.Common.ServiceResultBase;

namespace AscendantEmpire.Application.Features.DatabaseSeeding.Handlers;

/// <summary>
/// Creates a <see cref="Processing"/> record for a database seed and enqueues it for background processing.
/// </summary>
/// <remarks>
/// Returns the new <c>ProcessingId</c> so the caller can poll <c>GET /system/status</c> for progress. The
/// seed takes no inputs — all values come from the static seed-data classes read by the executor.
/// </remarks>
public record InitiateDatabaseSeedRequest() : IRequest<ServiceResult<int>>;

internal class InitiateDatabaseSeedHandler(
    ILogger<InitiateDatabaseSeedHandler> logger,
    AscendantEmpireContext context,
    IBackgroundProcessingChannel processingChannel
) : IRequestHandler<InitiateDatabaseSeedRequest, ServiceResult<int>>
{
    public async Task<ServiceResult<int>> Handle(InitiateDatabaseSeedRequest request, CancellationToken cancellationToken)
    {
        try
        {
            // Persist a Pending record so the caller immediately has a processId to poll. The executor needs
            // no metadata — its inputs are the static seed-data classes.
            var processing = new Processing
            {
                ProcessType = ProcessType.SeedDatabase,
                Status = ProcessingStatus.Pending,
                // StartedAt is overwritten when processing begins; set now so the column satisfies its non-null constraint.
                StartedAt = DateTime.UtcNow
            };

            context.Processing.Add(processing);
            await context.SaveChangesAsync(cancellationToken);

            await processingChannel.Writer.WriteAsync(processing.ProcessingId, cancellationToken);

            logger.LogInformation("Database seed job enqueued. ProcessingId: {ProcessingId}", processing.ProcessingId);

            return ServiceResult<int>.Created(processing.ProcessingId);
        }
        catch (Exception ex)
        {
            const string error = "An error occurred while initiating the database seed.";
            logger.LogError(ex, error);
            return ServiceResult<int>.InternalServerError(error);
        }
    }
}
