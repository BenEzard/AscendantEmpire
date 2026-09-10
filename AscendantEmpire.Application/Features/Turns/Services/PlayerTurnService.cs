using AscendantEmpire.Application.Common;
using AscendantEmpire.Application.Data;
using AscendantEmpire.Domain.Entities.Turns;

using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

using static AscendantEmpire.Application.Common.ServiceResultBase;

namespace AscendantEmpire.Application.Features.Turns.Services;

/// <inheritdoc cref="IPlayerTurnService" />
internal class PlayerTurnService(
    ILogger<PlayerTurnService> logger,
    IDbContextFactory<AscendantEmpireContext> dbFactory) : IPlayerTurnService
{
    /// <summary>
    /// Increments a Turn's counter and reads the new value back in one statement.
    /// </summary>
    /// <remarks>
    /// <list type="bullet">
    /// <item>
    /// <c>AS [Value]</c> is not decoration. EF's scalar <c>SqlQuery</c> shaper looks for a column literally named <c>Value</c> and throws without one.
    /// </item>
    /// <item>
    /// The single UPDATE takes an exclusive row lock for its own duration, so concurrent callers serialise and each gets a distinct, increasing value with
    /// no retry loop and one round-trip. That is the whole reason for dropping to raw SQL: ExecuteUpdateAsync cannot return the new value, and
    /// load-increment-save would need two round-trips and a concurrency retry that degrades exactly where it matters.
    /// </item>
    /// <item>
    /// <c>EndedAt IS NULL</c> makes a closed Turn match no rows, which is how the guard in <see cref="AllocateSequenceAsync(AscendantEmpireContext, int, int, CancellationToken)"/>
    /// tells "already ended" from "does not exist" - it cannot, and does not need to. Both are refusals.
    /// </item>
    /// </list>
    /// </remarks>
    private const string _ALLOCATE_SEQUENCE_SQL = """
        UPDATE turns.PlayerTurn
        SET LastAllocatedSequence = LastAllocatedSequence + @count
        OUTPUT INSERTED.LastAllocatedSequence AS [Value]
        WHERE Id = @playerTurnId
          AND EndedAt IS NULL;
        """;

    /// <inheritdoc />
    public async Task<ServiceResult<int>> OpenPlayerTurnAsync(
        AscendantEmpireContext context,
        int turnQueueId,
        int playerId,
        int roundNumber,
        CancellationToken cancelToken = default)
    {
        try
        {
            // Whatever the lane currently has open, if anything. Tracked rather than projected, because the mend below writes to it.
            PlayerTurn? openTurn = await context.PlayerTurns
                .Where(t => t.TurnQueueId == turnQueueId)
                .Where(t => t.EndedAt == null)
                .SingleOrDefaultAsync(cancelToken);

            if (openTurn is not null)
            {
                // The same Player, still in the same Round: a retried or half-failed start of Turn. Hand back the row they already have rather than
                // colliding with UX_PlayerTurn_OpenTurnPerQueue.
                if (openTurn.PlayerId == playerId && openTurn.RoundNumber == roundNumber)
                {
                    logger.LogInformation(
                        "Turn Queue Id {TurnQueueId} already has an open Player Turn Id {PlayerTurnId} for Player Id {PlayerId} in Round {RoundNumber}, so it has been reused.",
                        turnQueueId, openTurn.Id, playerId, roundNumber);

                    return ServiceResult<int>.Succeed(openTurn.Id);
                }

                // Somebody else's Turn, or an earlier Round, is still open. That should not happen, but refusing here would jam the lane on the unique index
                // for good - a Game that could never hand the Turn on again. Close the stale row loudly and carry on.
                logger.LogWarning(
                    "Turn Queue Id {TurnQueueId} still had Player Turn Id {PlayerTurnId} open for Player Id {StalePlayerId} in Round {StaleRoundNumber} when "
                        + "Player Id {PlayerId} started Round {RoundNumber}. The stale Turn has been closed.",
                    turnQueueId, openTurn.Id, openTurn.PlayerId, openTurn.RoundNumber, playerId, roundNumber);

                openTurn.EndedAt = DateTimeOffset.UtcNow;
            }

            var newTurn = new PlayerTurn
            {
                TurnQueueId = turnQueueId,
                PlayerId = playerId,
                RoundNumber = roundNumber,
                StartedAt = DateTimeOffset.UtcNow

                // EndedAt stays null - that is what "open" means - and LastAllocatedSequence takes its C# default of 0, meaning nothing recorded yet.
            };

            context.PlayerTurns.Add(newTurn);

            // Saving through the caller's context also flushes anything else they have tracked. Nothing does today - the callers only ever read the Queue and
            // its Participants - but it is worth knowing before somebody mutates one of those and is surprised at *when* it saved.
            await context.SaveChangesAsync(cancelToken);

            return ServiceResult<int>.Succeed(newTurn.Id);
        }
        catch (Exception ex)
        {
            string error = $"An error occurred while opening a Player Turn for Turn Queue Id {turnQueueId}, Player Id {playerId}, Round {roundNumber}.";
            logger.LogError(ex, "{Error}", error);

            return ServiceResult<int>.Fail(error);
        }
    }

    /// <inheritdoc />
    public async Task<ServiceResultBase> ClosePlayerTurnAsync(
        AscendantEmpireContext context,
        int turnQueueId,
        int expectedPlayerId,
        CancellationToken cancelToken = default)
    {
        try
        {
            var openTurn = await context.PlayerTurns
                .Where(t => t.TurnQueueId == turnQueueId)
                .Where(t => t.EndedAt == null)
                .Select(t => new { t.Id, t.PlayerId })
                .SingleOrDefaultAsync(cancelToken);

            if (openTurn is null)
            {
                // A refusal, not a no-op. Every Turn is opened before its start-of-turn pipeline runs, so reaching the end of one with nothing open means something
                // handed the Turn on without opening a row. Carrying on would lose that Turn from the record, along with every feed row it should have owned.
                string error = $"Turn Queue Id {turnQueueId} has no open Player Turn to close for Player Id {expectedPlayerId}.";
                logger.LogError("{Error}", error);

                return ServiceResultBase.Fail(error);
            }

            if (openTurn.PlayerId != expectedPlayerId)
            {
                // The lane's Turn is ending either way - there is only one, and it is over. Say so loudly, because it means something upstream handed the
                // Turn on without opening a row for the Player who then took it.
                logger.LogWarning(
                    "Turn Queue Id {TurnQueueId} had Player Turn Id {PlayerTurnId} open for Player Id {OpenPlayerId}, but Player Id {ExpectedPlayerId} was "
                        + "ending their Turn. The open Turn has been closed.",
                    turnQueueId, openTurn.Id, openTurn.PlayerId, expectedPlayerId);
            }

            DateTimeOffset now = DateTimeOffset.UtcNow;

            // LastAllocatedSequence is deliberately left alone. It is history - what the Turn handed out - not state that needs resetting.
            int updated = await context.PlayerTurns
                .Where(t => t.Id == openTurn.Id)
                .ExecuteUpdateAsync(setters => setters
                    .SetProperty(t => t.EndedAt, now),
                    cancelToken);

            if (updated == 0)
            {
                string error = $"Failed to close Player Turn Id {openTurn.Id} for Turn Queue Id {turnQueueId}.";
                logger.LogWarning("{Error}", error);

                return ServiceResultBase.Fail(error);
            }
        }
        catch (Exception ex)
        {
            string error = $"An error occurred while closing the open Player Turn for Turn Queue Id {turnQueueId}, Player Id {expectedPlayerId}.";
            logger.LogError(ex, "{Error}", error);

            return ServiceResultBase.Fail(error);
        }

        return ServiceResultBase.Succeed();
    }

    /// <inheritdoc />
    public async Task<ServiceResult<TurnSequenceBlock>> AllocateSequenceAsync(
        AscendantEmpireContext context,
        int playerTurnId,
        int count = 1,
        CancellationToken cancelToken = default)
    {
        if (count <= 0)
        {
            string countError = $"Sequence allocation count must be greater than zero. Provided count: {count}.";
            logger.LogWarning("{Error} Player Turn Id {PlayerTurnId}.", countError, playerTurnId);

            return ServiceResult<TurnSequenceBlock>.Fail(countError);
        }

        try
        {
            // Materialised with ToListAsync rather than FirstOrDefaultAsync on purpose. Composing over SqlQueryRaw makes EF wrap the statement as
            // SELECT ... FROM (<sql>) AS s, which is not valid SQL around an UPDATE.
            List<int> allocated = await context.Database
                .SqlQueryRaw<int>(_ALLOCATE_SEQUENCE_SQL,
                    new SqlParameter("@count", count),
                    new SqlParameter("@playerTurnId", playerTurnId))
                .ToListAsync(cancelToken);

            if (allocated.Count == 0)
            {
                // No rows matched, so either the Turn does not exist or it has already been closed. Both are refusals, and neither is "allocated nothing":
                // appending a feed row to a Turn whose fold has already run is intelligence that silently never arrives.
                string error = $"Player Turn Id {playerTurnId} does not exist or has already ended, so no sequence numbers were allocated.";
                logger.LogWarning("{Error}", error);

                return ServiceResult<TurnSequenceBlock>.Fail(error);
            }

            // The counter is a high-water mark, so what comes back is the *last* number of the run.
            int last = allocated[0];

            return ServiceResult<TurnSequenceBlock>.Succeed(new TurnSequenceBlock(last - count + 1, last));
        }
        catch (Exception ex)
        {
            string error = $"An error occurred while allocating {count} sequence number(s) from Player Turn Id {playerTurnId}.";
            logger.LogError(ex, "{Error}", error);

            return ServiceResult<TurnSequenceBlock>.Fail(error);
        }
    }

    /// <inheritdoc />
    public async Task<ServiceResult<TurnSequenceBlock>> AllocateSequenceAsync(int playerTurnId, int count = 1, CancellationToken cancelToken = default)
    {
        await using AscendantEmpireContext context = await dbFactory.CreateDbContextAsync(cancelToken);

        return await AllocateSequenceAsync(context, playerTurnId, count, cancelToken);
    }
}
