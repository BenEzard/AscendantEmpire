using AscendantEmpire.Application.BackgroundServices;
using AscendantEmpire.Application.Common.Validation;
using AscendantEmpire.Application.Data;
using AscendantEmpire.Application.Features.Game.Classes;
using AscendantEmpire.Domain.Entities.BackgroundProcessing;
using AscendantEmpire.Domain.Enums.Processing;
using AscendantEmpire.Domain.Enums.System;

using MediatR;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

using static AscendantEmpire.Application.Common.ServiceResultBase;

namespace AscendantEmpire.Application.Features.Game.Handlers;

/// <summary>
/// Creates a <see cref="Processing"/> record for a Game deletion and enqueues it for background processing.
/// </summary>
/// <remarks>
/// <para>
/// Returns the new <c>ProcessingId</c> so the caller can poll <c>GET /system/status</c> while the purge runs. The work itself is done by
/// <c>GameDeleteExecutor</c>; this handler only decides whether the job should be created at all.
/// </para>
/// <para>
/// The deletion is not restricted by the Game's status. A game that is part-built, mid-play or abandoned is exactly the kind an administrator needs to be
/// able to remove, and refusing any of those would leave the wreckage no way out.
/// </para>
/// </remarks>
public record InitiateGameDeleteRequest(int GameId) : IRequest<ServiceResult<int>>;

internal class InitiateGameDeleteRequestValidator : Validator<InitiateGameDeleteRequest>
{
    public InitiateGameDeleteRequestValidator()
    {
        RuleFor(x => x.GameId)
            .GreaterThan(0).WithMessage("Game Id is required.");
    }
}

internal class InitiateGameDeleteHandler(
    ILogger<InitiateGameDeleteHandler> logger,
    AscendantEmpireContext context,
    IBackgroundProcessingChannel processingChannel
) : IRequestHandler<InitiateGameDeleteRequest, ServiceResult<int>>
{
    public async Task<ServiceResult<int>> Handle(InitiateGameDeleteRequest request, CancellationToken cancellationToken)
    {
        try
        {
            bool gameExists = await context.GameIndex.AnyAsync(g => g.Id == request.GameId, cancellationToken);

            if (gameExists == false)
            {
                return ServiceResult<int>.NotFound($"Game {request.GameId} was not found.");
            }

            if (await IsAlreadyBeingDeletedAsync(request.GameId, cancellationToken))
            {
                return ServiceResult<int>.Conflict($"Game {request.GameId} is already being deleted.");
            }

            // Persist a Pending record so the caller immediately has a processId to poll. The executor reads
            // the target game back out of MetadataJson.
            var processing = new Processing
            {
                ProcessType = ProcessType.DeleteGame,
                Status = ProcessingStatus.Pending,
                // StartedAt is overwritten when processing begins; set now so the column satisfies its non-null constraint.
                StartedAt = DateTime.UtcNow,
                MetadataJson = GameDeleteMetadata.Write(request.GameId)
            };

            context.Processing.Add(processing);
            await context.SaveChangesAsync(cancellationToken);

            await processingChannel.Writer.WriteAsync(processing.ProcessingId, cancellationToken);

            logger.LogInformation(
                "Game delete job enqueued. ProcessingId: {ProcessingId}, GameId: {GameId}",
                processing.ProcessingId, request.GameId);

            return ServiceResult<int>.Created(processing.ProcessingId);
        }
        catch (Exception ex)
        {
            const string error = "An error occurred while initiating the game deletion.";
            logger.LogError(ex, error);
            return ServiceResult<int>.InternalServerError(error);
        }
    }

    /// <summary>
    /// Whether a delete job for this Game has already been queued or is already running.
    /// </summary>
    /// <remarks>
    /// Two purges of the same Game racing each other would have the second one deleting rows out from under the first, so the second attempt is refused.
    /// The target lives inside <c>MetadataJson</c>, which is opaque to the database, so the small set of unfinished delete jobs is read into memory and
    /// filtered there — there are never more than a handful, and it keeps the query provider-agnostic.
    /// </remarks>
    private async Task<bool> IsAlreadyBeingDeletedAsync(int gameId, CancellationToken cancellationToken)
    {
        List<string?> unfinishedDeleteJobs = await context.Processing
            .AsNoTracking()
            .Where(p => p.ProcessType == ProcessType.DeleteGame)
            .Where(p => p.Status == ProcessingStatus.Pending || p.Status == ProcessingStatus.InProgress)
            .Select(p => p.MetadataJson)
            .ToListAsync(cancellationToken);

        return unfinishedDeleteJobs.Any(metadata => GameDeleteMetadata.TryRead(metadata) == gameId);
    }
}
