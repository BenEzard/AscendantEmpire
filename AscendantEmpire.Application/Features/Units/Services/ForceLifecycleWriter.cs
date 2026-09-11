using AscendantEmpire.Application.Common;
using AscendantEmpire.Application.Data;
using AscendantEmpire.Application.Features.Turns.Services;
using AscendantEmpire.Domain.Entities.Units;
using AscendantEmpire.Domain.Enums.Forces;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.Logging;

using static AscendantEmpire.Application.Common.ServiceResultBase;

namespace AscendantEmpire.Application.Features.Units.Services;

/// <inheritdoc cref="IForceLifecycleWriter" />
internal class ForceLifecycleWriter(
    ILogger<ForceLifecycleWriter> logger,
    IPlayerTurnService playerTurnService) : IForceLifecycleWriter
{
    /// <inheritdoc />
    public async Task<ServiceResultBase> MoveAsync(AscendantEmpireContext context, UnitInstance force, int destinationTileId, int playerTurnId, CancellationToken cancelToken = default)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(force);

        try
        {
            EntityEntry<UnitInstance> entry = context.Entry(force);

            ServiceResultBase? trackingRefusal = RefuseUnlessTrackedAsExisting(entry, "moved");
            if (trackingRefusal is not null)
            {
                return trackingRefusal;
            }

            // Read through the tracker rather than off the entity, which is the same value today but is the one the tracker will save.
            int originTileId = entry.Property(f => f.CampaignMapTileId).CurrentValue;

            if (originTileId == destinationTileId)
            {
                string error = $"Unit Instance Id {force.Id} is already on Campaign Map Tile Id {destinationTileId}, so there is no move to record.";
                logger.LogWarning("{Error}", error);

                return ServiceResultBase.Fail(error);
            }

            // Allocated before anything is staged, so a refusal leaves the context exactly as the caller handed it over.
            ServiceResult<TurnSequenceBlock> sequenceResult = await playerTurnService.AllocateSequenceAsync(context, playerTurnId, count: 1, cancelToken);

            if (sequenceResult.HasSucceeded == false)
            {
                return RefuseForMissingSequence(sequenceResult, "move", force.Id, playerTurnId);
            }

            // The property is init-only, so this is the one place a tracked Force's tile can be changed. Setting it through the entry also marks it modified for the caller's save.
            entry.Property(f => f.CampaignMapTileId).CurrentValue = destinationTileId;

            context.MovementHistory.Add(BuildRow(
                force,
                ForceHistoryEventType.Moved,
                fromTileId: originTileId,
                tileId: destinationTileId,
                playerTurnId: playerTurnId,
                turnSequence: sequenceResult.SuccessValue.First));

            return ServiceResultBase.Succeed();
        }
        catch (Exception ex)
        {
            string error = $"An error occurred while moving Unit Instance Id {force.Id} to Campaign Map Tile Id {destinationTileId}.";
            logger.LogError(ex, "{Error}", error);

            return ServiceResultBase.Fail(error);
        }
    }

    /// <inheritdoc />
    public async Task<ServiceResultBase> CreateAsync(AscendantEmpireContext context, IReadOnlyCollection<UnitInstance> forces, int? playerTurnId, CancellationToken cancelToken = default)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(forces);

        if (forces.Count == 0)
        {
            return ServiceResultBase.Succeed();
        }

        // An id means the Force has been inserted already. Adding it again would try to insert a row with an explicit identity value and fail in the database, which is a worse place to learn it.
        if (forces.Any(force => force.Id != 0))
        {
            string error = "Only new Forces can be created. Forces that already have an id exist in the database.";
            logger.LogError("{Error} Unit Instance Ids: {UnitInstanceIds}", error, string.Join(", ", forces.Where(force => force.Id != 0).Select(force => force.Id)));

            return ServiceResultBase.Fail(error);
        }

        try
        {
            // Placed outside any Turn: nothing to stamp a row with, and nobody who could have watched. One ordinary save, and no transaction for it to need.
            if (playerTurnId is not int turnId)
            {
                context.UnitInstances.AddRange(forces);
                await context.SaveChangesAsync(cancelToken);

                return ServiceResultBase.Succeed();
            }

            // Allocated before the transaction opens and before anything is added, so a refusal writes nothing. Where the caller already has a transaction open, the allocation joins it.
            ServiceResult<TurnSequenceBlock> sequenceResult = await playerTurnService.AllocateSequenceAsync(context, turnId, count: 1, cancelToken);

            if (sequenceResult.HasSucceeded == false)
            {
                return RefuseForMissingSequence(sequenceResult, "creation", unitInstanceId: null, turnId);
            }

            int turnSequence = sequenceResult.SuccessValue.First;

            // Two saves have to land together: the Forces, to learn their ids, then the rows carrying them. A caller already inside a transaction keeps ownership of it - committing or rolling back
            // their transaction from in here would take decisions about writes that are not ours. Otherwise this opens one, and disposing it uncommitted rolls both saves back.
            await using IDbContextTransaction? ownTransaction = context.Database.CurrentTransaction is null
                ? await context.Database.BeginTransactionAsync(cancelToken)
                : null;

            context.UnitInstances.AddRange(forces);
            await context.SaveChangesAsync(cancelToken);

            context.MovementHistory.AddRange(forces.Select(force => BuildRow(
                force,
                ForceHistoryEventType.Created,
                fromTileId: null,
                tileId: force.CampaignMapTileId,
                playerTurnId: turnId,
                turnSequence: turnSequence)));

            await context.SaveChangesAsync(cancelToken);

            if (ownTransaction is not null)
            {
                await ownTransaction.CommitAsync(cancelToken);
            }

            return ServiceResultBase.Succeed();
        }
        catch (Exception ex)
        {
            string error = $"An error occurred while creating {forces.Count} Force(s).";
            logger.LogError(ex, "{Error} Player Turn Id: {PlayerTurnId}.", error, playerTurnId);

            return ServiceResultBase.Fail(error);
        }
    }

    /// <inheritdoc />
    public async Task<ServiceResultBase> DestroyAsync(AscendantEmpireContext context, UnitInstance force, int playerTurnId, CancellationToken cancelToken = default)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(force);

        try
        {
            EntityEntry<UnitInstance> entry = context.Entry(force);

            ServiceResultBase? trackingRefusal = RefuseUnlessTrackedAsExisting(entry, "destroyed");
            if (trackingRefusal is not null)
            {
                return trackingRefusal;
            }

            ServiceResult<TurnSequenceBlock> sequenceResult = await playerTurnService.AllocateSequenceAsync(context, playerTurnId, count: 1, cancelToken);

            if (sequenceResult.HasSucceeded == false)
            {
                return RefuseForMissingSequence(sequenceResult, "destruction", force.Id, playerTurnId);
            }

            // The row is built before the Force is removed, while every value it copies is still to hand. It keeps the Force's id after the delete, since that column is not a foreign key.
            context.MovementHistory.Add(BuildRow(
                force,
                ForceHistoryEventType.Destroyed,
                fromTileId: null,
                tileId: entry.Property(f => f.CampaignMapTileId).CurrentValue,
                playerTurnId: playerTurnId,
                turnSequence: sequenceResult.SuccessValue.First));

            context.UnitInstances.Remove(force);

            return ServiceResultBase.Succeed();
        }
        catch (Exception ex)
        {
            string error = $"An error occurred while destroying Unit Instance Id {force.Id}.";
            logger.LogError(ex, "{Error}", error);

            return ServiceResultBase.Fail(error);
        }
    }

    /// <summary>
    /// Refuses a Force the context is not tracking as an existing row.
    /// </summary>
    /// <remarks>
    /// Unchanged or Modified are the only states that mean "a row in the database, tracked here". A detached Force would have its tile changed on an object the save never looks at, an added one has
    /// not been created - and so was not created through this writer - and a deleted one is already on its way out. Each of those is a caller bug, so it is logged as an error.
    /// </remarks>
    /// <param name="pastParticiple">How the refused action reads in the message - "moved", "destroyed".</param>
    /// <returns><see langword="null"/> when the Force is tracked as an existing row; otherwise the failure to answer with.</returns>
    private ServiceResultBase? RefuseUnlessTrackedAsExisting(EntityEntry<UnitInstance> entry, string pastParticiple)
    {
        if (entry.State is EntityState.Unchanged or EntityState.Modified)
        {
            return null;
        }

        string error = $"Unit Instance Id {entry.Entity.Id} cannot be {pastParticiple}: the context is tracking it as {entry.State}, not as an existing Force.";
        logger.LogError("{Error}", error);

        return ServiceResultBase.Fail(error);
    }

    /// <summary>
    /// Turns a failed sequence allocation into the writer's own refusal, keeping the allocator's reasons.
    /// </summary>
    /// <remarks>
    /// Logged as a warning rather than an error: the allocator has already logged its own failure, and the usual cause - a Turn that has just been closed - is a race rather than a fault.
    /// </remarks>
    private ServiceResultBase RefuseForMissingSequence(ServiceResult<TurnSequenceBlock> sequenceResult, string action, int? unitInstanceId, int playerTurnId)
    {
        string error = $"No sequence number could be allocated from Player Turn Id {playerTurnId}, so the {action} was not recorded and has not been made.";

        logger.LogWarning("{Error} Unit Instance Id: {UnitInstanceId}. Allocator errors: {Errors}", error, unitInstanceId, string.Join("; ", sequenceResult.Errors));

        return ServiceResultBase.Fail(sequenceResult.Errors.Prepend(error));
    }

    /// <summary>
    /// Builds one history row for a Force, copying the owner and template off it so the row still says what the Force was after it has gone.
    /// </summary>
    private static MovementHistory BuildRow(UnitInstance force, ForceHistoryEventType eventType, int? fromTileId, int tileId, int playerTurnId, int turnSequence)
    {
        return new MovementHistory
        {
            GameId = force.GameId,
            UnitInstanceId = force.Id,
            PlayerId = force.PlayerId,
            UnitTemplateId = force.UnitTemplateId,
            EventType = eventType,
            FromCampaignMapTileId = fromTileId,
            CampaignMapTileId = tileId,
            PlayerTurnId = playerTurnId,
            TurnSequence = turnSequence
        };
    }
}
