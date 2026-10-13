using AscendantEmpire.Application.BackgroundServices;
using AscendantEmpire.Application.Common.Messaging;
using AscendantEmpire.Application.Data;
using AscendantEmpire.Application.Features.DatabaseSeeding.Classes;
using AscendantEmpire.Application.Features.DatabaseSeeding.Steps;
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
/// Returns the new <c>ProcessingId</c> so the caller can poll <c>GET /system/status</c> for progress. The selection is expanded over its prerequisites here
/// and the expanded list is what the job carries, so the Processing row records what the seed will really write rather than what was ticked.
/// </remarks>
/// <param name="Targets">The parts of the database to seed. Must not be empty; prerequisites are added automatically.</param>
public record InitiateDatabaseSeedRequest(IReadOnlyCollection<SeedTarget> Targets) : IRequest<ServiceResult<int>>;

internal class InitiateDatabaseSeedHandler(
    ILogger<InitiateDatabaseSeedHandler> logger,
    AscendantEmpireContext context,
    IBackgroundProcessingChannel processingChannel,
    IEnumerable<ISeedStep> seedSteps
) : IRequestHandler<InitiateDatabaseSeedRequest, ServiceResult<int>>
{
    public async Task<ServiceResult<int>> Handle(InitiateDatabaseSeedRequest request, CancellationToken cancellationToken)
    {
        if (request.Targets.Count == 0)
        {
            return ServiceResult<int>.Fail("At least one seed target must be selected.");
        }

        IReadOnlyList<SeedTarget> expandedTargets;

        try
        {
            expandedTargets = SeedTargetResolver.ExpandTargets(seedSteps, request.Targets);
        }
        catch (InvalidOperationException ex)
        {
            // A target with no registered step is the only one of these a caller can cause; a cycle or a
            // duplicate registration is a coding error the step-catalogue tests catch first.
            logger.LogWarning(ex, "Could not resolve the requested seed targets.");
            return ServiceResult<int>.Fail(ex.Message);
        }

        // Persist a Pending record so the caller immediately has a processId to poll. The executor reads
        // the expanded target list back out of MetadataJson.
        var processing = new Processing
        {
            ProcessType = ProcessType.SeedDatabase,
            Status = ProcessingStatus.Pending,
            // StartedAt is overwritten when processing begins; set now so the column satisfies its non-null constraint.
            StartedAt = DateTime.UtcNow,
            MetadataJson = DatabaseSeedMetadata.Write(expandedTargets)
        };

        context.Processing.Add(processing);
        await context.SaveChangesAsync(cancellationToken);

        await processingChannel.Writer.WriteAsync(processing.ProcessingId, cancellationToken);

        logger.LogInformation(
            "Database seed job enqueued. ProcessingId: {ProcessingId}. Requested: {RequestedTargets}. Expanded: {ExpandedTargets}",
            processing.ProcessingId,
            string.Join(", ", request.Targets),
            string.Join(", ", expandedTargets));

        return ServiceResult<int>.Created(processing.ProcessingId);
    }
}
