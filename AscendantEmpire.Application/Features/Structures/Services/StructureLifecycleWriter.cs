using AscendantEmpire.Application.Common;
using AscendantEmpire.Application.Data;
using AscendantEmpire.Application.Features.Turns.Services;
using AscendantEmpire.Domain.Entities.Games;
using AscendantEmpire.Domain.Entities.Structures;
using AscendantEmpire.Domain.Enums.Structures;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.Logging;

using static AscendantEmpire.Application.Common.ServiceResultBase;

namespace AscendantEmpire.Application.Features.Structures.Services;

/// <inheritdoc cref="IStructureLifecycleWriter" />
internal class StructureLifecycleWriter(
    ILogger<StructureLifecycleWriter> logger,
    IPlayerTurnService playerTurnService) : IStructureLifecycleWriter
{
    /// <summary>
    /// The completion at which a Structure is finished.
    /// </summary>
    private const double _COMPLETE_PERCENT = 100d;

    /// <summary>
    /// <see cref="StructureInstance.CreatedOnTurnId"/> for a Structure placed outside any Turn - the same "no Turn" sentinel the campaign map's exploration stamp uses.
    /// </summary>
    private const int _NO_TURN_STAMP = 0;

    /// <inheritdoc />
    public async Task<ServiceResultBase> FoundAsync(AscendantEmpireContext context, StructureInstance structure, int? playerTurnId, CancellationToken cancelToken = default)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(structure);

        // An id means the Structure has been inserted already; adding it again would fail in the database, which is a worse place to learn it.
        if (structure.Id != 0)
        {
            return Refuse($"Structure Instance Id {structure.Id} already exists, so it cannot be founded.");
        }

        ServiceResultBase? campaignRefusal = RefuseUnlessOnCampaignMap(structure, "founded");
        if (campaignRefusal is not null)
        {
            return campaignRefusal;
        }

        if (structure.RemovedOnTurnId is not null)
        {
            return Refuse("A Structure cannot be founded already razed.");
        }

        // The kind carries the total the progress is measured against, so without it there is no saying whether this is a fresh foundation or a finished building.
        if (await ResolveBuildPointsRequiredAsync(context, structure, cancelToken) is not long buildPointsRequired)
        {
            return Refuse($"Structure Id {structure.StructureId} is not in the catalogue, so how much work its kind takes is unknown.");
        }

        if (structure.BuildPointsProgress < 0 || structure.BuildPointsProgress > buildPointsRequired)
        {
            return Refuse(
                $"A Structure cannot be founded with {structure.BuildPointsProgress} of {buildPointsRequired} build points done; its progress must be from 0 to its kind's total.");
        }

        // A kind that takes no work has no total to be founded at, so it is never founded complete: the end-of-Turn process finishes it during the Turn it appeared in.
        // That is what keeps "placed already finished" and "nothing to build" apart now that both would otherwise be 0 of 0.
        bool isFoundedComplete = buildPointsRequired > 0 && structure.BuildPointsProgress >= buildPointsRequired;

        try
        {
            // Placed outside any Turn: nothing to stamp a row with, and nobody who could have watched. One ordinary save, and no transaction for it to need.
            if (playerTurnId is not int turnId)
            {
                context.StructureInstances.Add(structure);
                StampFounding(context.Entry(structure), createdOnTurnId: _NO_TURN_STAMP, isFoundedComplete);

                await context.SaveChangesAsync(cancelToken);

                return ServiceResultBase.Succeed();
            }

            // One number for the founding, and a second straight after it when the Structure is finished at once. Allocated before anything is added, so a refusal writes nothing.
            int sequenceCount = isFoundedComplete ? 2 : 1;

            ServiceResult<TurnSequenceBlock> sequenceResult = await playerTurnService.AllocateSequenceAsync(context, turnId, sequenceCount, cancelToken);

            if (sequenceResult.HasSucceeded == false)
            {
                return RefuseForMissingSequence(sequenceResult, "founding", structureInstanceId: null, turnId);
            }

            // Two saves have to land together: the Structure, to learn its id, then the rows carrying it. A caller already inside a transaction keeps ownership of it; otherwise this opens one, and
            // disposing it uncommitted rolls both saves back.
            await using IDbContextTransaction? ownTransaction = context.Database.CurrentTransaction is null
                ? await context.Database.BeginTransactionAsync(cancelToken)
                : null;

            context.StructureInstances.Add(structure);
            StampFounding(context.Entry(structure), createdOnTurnId: turnId, isFoundedComplete);

            await context.SaveChangesAsync(cancelToken);

            context.StructureHistory.Add(BuildRow(
                structure,
                StructureHistoryEventType.Founded,
                ownerPlayerId: structure.OwnedByPlayerId,
                previousOwnerPlayerId: null,
                completionPercent: ToCompletionPercent(structure.BuildPointsProgress, buildPointsRequired),
                playerTurnId: turnId,
                turnSequence: sequenceResult.SuccessValue.First));

            if (isFoundedComplete)
            {
                context.StructureHistory.Add(BuildRow(
                    structure,
                    StructureHistoryEventType.Completed,
                    ownerPlayerId: structure.OwnedByPlayerId,
                    previousOwnerPlayerId: null,
                    completionPercent: _COMPLETE_PERCENT,
                    playerTurnId: turnId,
                    turnSequence: sequenceResult.SuccessValue.Last));
            }

            await context.SaveChangesAsync(cancelToken);

            if (ownTransaction is not null)
            {
                await ownTransaction.CommitAsync(cancelToken);
            }

            return ServiceResultBase.Succeed();
        }
        catch (Exception ex)
        {
            string error = $"An error occurred while founding Structure Id {structure.StructureId} on Campaign Map Tile Id {structure.CampaignMapTileId}.";
            logger.LogError(ex, "{Error} Player Turn Id: {PlayerTurnId}.", error, playerTurnId);

            return ServiceResultBase.Fail(error);
        }
    }

    /// <inheritdoc />
    public async Task<ServiceResultBase> ProgressAsync(AscendantEmpireContext context, StructureInstance structure, long buildPointsProgress, int playerTurnId, CancellationToken cancelToken = default)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(structure);

        try
        {
            EntityEntry<StructureInstance> entry = context.Entry(structure);

            ServiceResultBase? refusal = RefuseUnlessStandingAndTracked(entry, "built");
            if (refusal is not null)
            {
                return refusal;
            }

            if (await ResolveBuildPointsRequiredAsync(context, structure, cancelToken) is not long buildPointsRequired)
            {
                return Refuse($"Structure Id {structure.StructureId} is not in the catalogue, so how much work Structure Instance Id {structure.Id} takes is unknown.");
            }

            // Read through the tracker rather than off the entity, which is the same value today but is the one the tracker will save.
            long currentProgress = entry.Property(s => s.BuildPointsProgress).CurrentValue;

            // Either mark of being finished. The stamp is the authoritative one, and the only one a kind that takes no work ever gets, since its progress never moves;
            // the work comparison catches a Structure whose progress is already met but was never stamped.
            if (entry.Property(s => s.CompletedOnTurnId).CurrentValue is not null
                || (buildPointsRequired > 0 && currentProgress >= buildPointsRequired))
            {
                return Refuse($"Structure Instance Id {structure.Id} is already complete, so construction on it cannot advance.");
            }

            // A kind that takes no work is finished by the step that reaches it, though its progress never moves off 0. Everything else has to actually advance.
            if (buildPointsRequired > 0 && buildPointsProgress <= currentProgress)
            {
                return Refuse(
                    $"Structure Instance Id {structure.Id} has {currentProgress} of {buildPointsRequired} build points done, so {buildPointsProgress} does not advance its construction.");
            }

            // Not Math.Clamp, which throws when its bounds cross - and they would on a Structure already past a total the catalogue has since lowered.
            long newProgress = Math.Min(Math.Max(buildPointsProgress, currentProgress), buildPointsRequired);

            // An exact comparison, which is the whole point of storing work rather than a percentage: there is no rounding gap to close, and no step that has to look
            // ahead to decide whether the one after it would overshoot - the bug that made a build finish a Turn early whenever the work did not divide evenly.
            bool isNowComplete = newProgress >= buildPointsRequired;

            // Allocated before anything is staged, so a refusal leaves the context exactly as the caller handed it over.
            ServiceResult<TurnSequenceBlock> sequenceResult = await playerTurnService.AllocateSequenceAsync(context, playerTurnId, count: 1, cancelToken);

            if (sequenceResult.HasSucceeded == false)
            {
                return RefuseForMissingSequence(sequenceResult, "construction", structure.Id, playerTurnId);
            }

            // Both properties are init-only, so this is the one place a tracked Structure's construction can be changed. Setting them through the entry marks them modified for the caller's save.
            entry.Property(s => s.BuildPointsProgress).CurrentValue = newProgress;

            if (isNowComplete)
            {
                entry.Property(s => s.CompletedOnTurnId).CurrentValue = playerTurnId;
            }

            context.StructureHistory.Add(BuildRow(
                structure,
                isNowComplete ? StructureHistoryEventType.Completed : StructureHistoryEventType.Progressed,
                ownerPlayerId: entry.Property(s => s.OwnedByPlayerId).CurrentValue,
                previousOwnerPlayerId: null,
                // A finished Structure reads 100 whatever its kind's total, which is what keeps a zero-work kind from recording its completion as 0%.
                completionPercent: isNowComplete ? _COMPLETE_PERCENT : ToCompletionPercent(newProgress, buildPointsRequired),
                playerTurnId: playerTurnId,
                turnSequence: sequenceResult.SuccessValue.First));

            return ServiceResultBase.Succeed();
        }
        catch (Exception ex)
        {
            string error = $"An error occurred while advancing construction on Structure Instance Id {structure.Id} to {buildPointsProgress} build points.";
            logger.LogError(ex, "{Error}", error);

            return ServiceResultBase.Fail(error);
        }
    }

    /// <inheritdoc />
    public async Task<ServiceResultBase> CaptureAsync(AscendantEmpireContext context, StructureInstance structure, int newOwnerPlayerId, int playerTurnId, CancellationToken cancelToken = default)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(structure);

        try
        {
            EntityEntry<StructureInstance> entry = context.Entry(structure);

            ServiceResultBase? refusal = RefuseUnlessStandingAndTracked(entry, "captured");
            if (refusal is not null)
            {
                return refusal;
            }

            // Only to work out the percentage the row snapshots; a capture does not touch how far built the Structure is.
            long buildPointsRequired = await ResolveBuildPointsRequiredAsync(context, structure, cancelToken) ?? 0;

            int previousOwnerPlayerId = entry.Property(s => s.OwnedByPlayerId).CurrentValue;

            // A row whose old and new owners matched would tell the fold a Structure changed hands when nothing happened.
            if (previousOwnerPlayerId == newOwnerPlayerId)
            {
                return Refuse($"Structure Instance Id {structure.Id} already belongs to Player Id {newOwnerPlayerId}, so there is no capture to record.");
            }

            ServiceResult<TurnSequenceBlock> sequenceResult = await playerTurnService.AllocateSequenceAsync(context, playerTurnId, count: 1, cancelToken);

            if (sequenceResult.HasSucceeded == false)
            {
                return RefuseForMissingSequence(sequenceResult, "capture", structure.Id, playerTurnId);
            }

            entry.Property(s => s.OwnedByPlayerId).CurrentValue = newOwnerPlayerId;

            context.StructureHistory.Add(BuildRow(
                structure,
                StructureHistoryEventType.Captured,
                ownerPlayerId: newOwnerPlayerId,
                previousOwnerPlayerId: previousOwnerPlayerId,
                completionPercent: CompletionPercentOf(entry, buildPointsRequired),
                playerTurnId: playerTurnId,
                turnSequence: sequenceResult.SuccessValue.First));

            return ServiceResultBase.Succeed();
        }
        catch (Exception ex)
        {
            string error = $"An error occurred while capturing Structure Instance Id {structure.Id} for Player Id {newOwnerPlayerId}.";
            logger.LogError(ex, "{Error}", error);

            return ServiceResultBase.Fail(error);
        }
    }

    /// <inheritdoc />
    public async Task<ServiceResultBase> RazeAsync(AscendantEmpireContext context, StructureInstance structure, int playerTurnId, CancellationToken cancelToken = default)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(structure);

        try
        {
            EntityEntry<StructureInstance> entry = context.Entry(structure);

            ServiceResultBase? refusal = RefuseUnlessStandingAndTracked(entry, "razed");
            if (refusal is not null)
            {
                return refusal;
            }

            // As in CaptureAsync: needed only for the percentage the row snapshots.
            long buildPointsRequired = await ResolveBuildPointsRequiredAsync(context, structure, cancelToken) ?? 0;

            ServiceResult<TurnSequenceBlock> sequenceResult = await playerTurnService.AllocateSequenceAsync(context, playerTurnId, count: 1, cancelToken);

            if (sequenceResult.HasSucceeded == false)
            {
                return RefuseForMissingSequence(sequenceResult, "razing", structure.Id, playerTurnId);
            }

            // The soft delete. The row stays, so the history, report and known-structure rows pointing at it keep their foreign keys, and the filtered slot index lets a new Structure take the tile.
            entry.Property(s => s.RemovedOnTurnId).CurrentValue = playerTurnId;

            context.StructureHistory.Add(BuildRow(
                structure,
                StructureHistoryEventType.Razed,
                ownerPlayerId: entry.Property(s => s.OwnedByPlayerId).CurrentValue,
                previousOwnerPlayerId: null,
                completionPercent: CompletionPercentOf(entry, buildPointsRequired),
                playerTurnId: playerTurnId,
                turnSequence: sequenceResult.SuccessValue.First));

            return ServiceResultBase.Succeed();
        }
        catch (Exception ex)
        {
            string error = $"An error occurred while razing Structure Instance Id {structure.Id}.";
            logger.LogError(ex, "{Error}", error);

            return ServiceResultBase.Fail(error);
        }
    }

    /// <summary>
    /// How far built a tracked Structure is, as the percentage the history and intelligence snapshots record.
    /// </summary>
    /// <remarks>
    /// Read through the tracker, so a row written alongside a change in the same call carries the value that change is about to save. A finished Structure reads 100
    /// whatever its kind's total: that is what keeps a kind which takes no work - a marker rather than a building - from recording itself as 0% for ever once it is up.
    /// </remarks>
    private static double CompletionPercentOf(EntityEntry<StructureInstance> entry, long buildPointsRequired)
    {
        if (entry.Property(s => s.CompletedOnTurnId).CurrentValue is not null)
        {
            return _COMPLETE_PERCENT;
        }

        return ToCompletionPercent(entry.Property(s => s.BuildPointsProgress).CurrentValue, buildPointsRequired);
    }

    /// <summary>
    /// How much work the Structure's kind takes, from the loaded navigation where the caller has one and from the catalogue otherwise.
    /// </summary>
    /// <remarks>
    /// Preferring the navigation keeps the end-of-Turn process, which includes its kinds already, from paying for a query per Structure; falling back to one means no
    /// caller has to know that it must include anything. <see langword="null"/> is a Structure whose kind is not in the catalogue at all.
    /// </remarks>
    private static async Task<long?> ResolveBuildPointsRequiredAsync(AscendantEmpireContext context, StructureInstance structure, CancellationToken cancelToken)
    {
        if (structure.Structure is not null)
        {
            return structure.Structure.BuildPointsRequired;
        }

        return await context.Structures
            .Where(kind => kind.Id == structure.StructureId)
            .Select(kind => (long?)kind.BuildPointsRequired)
            .FirstOrDefaultAsync(cancelToken);
    }

    /// <summary>
    /// Turns work done out of work required into the percentage the snapshots carry.
    /// </summary>
    /// <remarks>
    /// A kind that takes no work reads 0 here rather than dividing by zero. It is complete or it is not, and <see cref="StructureInstance.CompletedOnTurnId"/> is what
    /// says which - see <see cref="CompletionPercentOf"/>.
    /// </remarks>
    private static double ToCompletionPercent(long buildPointsProgress, long buildPointsRequired)
    {
        if (buildPointsRequired <= 0)
        {
            return 0d;
        }

        return Math.Min(_COMPLETE_PERCENT, _COMPLETE_PERCENT * buildPointsProgress / buildPointsRequired);
    }

    /// <summary>
    /// Stamps a newly added Structure with the Turn it was founded in, and with the Turn it was finished in when it is founded complete.
    /// </summary>
    /// <remarks>
    /// <see cref="StructureInstance.CompletedOnTurnId"/> is init-only, so it is set through the entry, overwriting whatever the caller put there: this writer decides it.
    /// </remarks>
    private static void StampFounding(EntityEntry<StructureInstance> entry, int createdOnTurnId, bool isFoundedComplete)
    {
        entry.Entity.CreatedOnTurnId = createdOnTurnId;
        entry.Property(s => s.CompletedOnTurnId).CurrentValue = isFoundedComplete ? createdOnTurnId : null;
    }

    /// <summary>
    /// Refuses a Structure that stands on a battle map.
    /// </summary>
    /// <param name="pastParticiple">How the refused action reads in the message - "founded", "razed".</param>
    /// <returns><see langword="null"/> when the Structure is on the campaign map; otherwise the failure to answer with.</returns>
    private ServiceResultBase? RefuseUnlessOnCampaignMap(StructureInstance structure, string pastParticiple)
    {
        if (structure.BattleMapTileId is null)
        {
            return null;
        }

        return Refuse($"Structure Instance Id {structure.Id} is on Battle Map Tile Id {structure.BattleMapTileId} and cannot be {pastParticiple} through the campaign map's history.");
    }

    /// <summary>
    /// Refuses a Structure that the context is not tracking as an existing row, that stands on a battle map, or that has already been razed.
    /// </summary>
    /// <remarks>
    /// Unchanged or Modified are the only states that mean "a row in the database, tracked here". A detached Structure would be changed on an object the save never looks at, an added one was not
    /// founded through this writer, and a deleted one is on its way out - each a caller bug, so logged as an error. A razed Structure is an ordinary refusal: it can no longer be changed.
    /// </remarks>
    /// <param name="pastParticiple">How the refused action reads in the message - "built", "captured", "razed".</param>
    /// <returns><see langword="null"/> when the Structure may be changed; otherwise the failure to answer with.</returns>
    private ServiceResultBase? RefuseUnlessStandingAndTracked(EntityEntry<StructureInstance> entry, string pastParticiple)
    {
        if (entry.State is not (EntityState.Unchanged or EntityState.Modified))
        {
            string error = $"Structure Instance Id {entry.Entity.Id} cannot be {pastParticiple}: the context is tracking it as {entry.State}, not as an existing Structure.";
            logger.LogError("{Error}", error);

            return ServiceResultBase.Fail(error);
        }

        ServiceResultBase? campaignRefusal = RefuseUnlessOnCampaignMap(entry.Entity, pastParticiple);
        if (campaignRefusal is not null)
        {
            return campaignRefusal;
        }

        if (entry.Property(s => s.RemovedOnTurnId).CurrentValue is int removedOnTurnId)
        {
            return Refuse($"Structure Instance Id {entry.Entity.Id} was razed during Player Turn Id {removedOnTurnId} and cannot be {pastParticiple}.");
        }

        return null;
    }

    /// <summary>
    /// Logs and returns a refusal of a change the writer will not make.
    /// </summary>
    private ServiceResultBase Refuse(string error)
    {
        logger.LogWarning("{Error}", error);

        return ServiceResultBase.Fail(error);
    }

    /// <summary>
    /// Turns a failed sequence allocation into the writer's own refusal, keeping the allocator's reasons.
    /// </summary>
    /// <remarks>
    /// Logged as a warning rather than an error: the allocator has already logged its own failure, and the usual cause - a Turn that has just been closed - is a race rather than a fault.
    /// </remarks>
    private ServiceResultBase RefuseForMissingSequence(ServiceResult<TurnSequenceBlock> sequenceResult, string action, int? structureInstanceId, int playerTurnId)
    {
        string error = $"No sequence number could be allocated from Player Turn Id {playerTurnId}, so the {action} was not recorded and has not been made.";

        logger.LogWarning("{Error} Structure Instance Id: {StructureInstanceId}. Allocator errors: {Errors}", error, structureInstanceId, string.Join("; ", sequenceResult.Errors));

        return ServiceResultBase.Fail(sequenceResult.Errors.Prepend(error));
    }

    /// <summary>
    /// Builds one history row for a Structure, copying its kind and tile so the row says what and where it was without a join.
    /// </summary>
    private static StructureHistory BuildRow(
        StructureInstance structure,
        StructureHistoryEventType eventType,
        int ownerPlayerId,
        int? previousOwnerPlayerId,
        double completionPercent,
        int playerTurnId,
        int turnSequence)
    {
        return new StructureHistory
        {
            GameId = structure.GameId,
            StructureInstanceId = structure.Id,
            StructureId = structure.StructureId,
            PlayerId = ownerPlayerId,
            PreviousOwnerPlayerId = previousOwnerPlayerId,
            EventType = eventType,
            CompletionPercent = completionPercent,
            CampaignMapTileId = structure.CampaignMapTileId,
            PlayerTurnId = playerTurnId,
            TurnSequence = turnSequence
        };
    }
}
