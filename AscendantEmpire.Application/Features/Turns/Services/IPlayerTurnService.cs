using AscendantEmpire.Application.Common;
using AscendantEmpire.Application.Data;
using AscendantEmpire.Domain.Entities.Turns;

using static AscendantEmpire.Application.Common.ServiceResultBase;

namespace AscendantEmpire.Application.Features.Turns.Services;

/// <summary>
/// An inclusive run of sequence numbers reserved for one writer.
/// </summary>
/// <remarks><see cref="First"/> and <see cref="Last"/> are the same value for a single allocation.</remarks>
public readonly record struct TurnSequenceBlock(int First, int Last);

/// <summary>
/// Opens and closes <see cref="PlayerTurn"/> rows, and hands out the sequence numbers the raw activity feeds order themselves by.
/// </summary>
/// <remarks>
/// Lane-agnostic on purpose: every method is keyed on a Turn Queue or a Turn, never on a Game or a queue type, so the Battle lane can open Turns through it
/// unchanged once battle pipelines exist. The campaign-specific lookup lives on <see cref="ICampaignTurnService"/>, which already owns the campaign lane
/// predicate.
/// <para>
/// See <c>docs/design/intelligence-and-sightings.md</c> for what the sequence numbers are for.
/// </para>
/// </remarks>
public interface IPlayerTurnService
{
    /// <summary>
    /// Opens the Turn for a Player in a lane, and returns its id.
    /// </summary>
    /// <remarks>
    /// Idempotent, and self-healing. If the lane already has an open Turn belonging to this Player in this Round, its id comes back and nothing is inserted -
    /// which is what makes a retried or half-failed start-of-turn safe. If the open Turn belongs to somebody else, or to an earlier Round, it is closed with
    /// a warning first: a lane may only have one open Turn, and refusing here would jam the Game rather than mend it.
    /// <para>
    /// Reads and writes through the caller's context so it joins their transaction. That matters for a Game starting for the first time, which runs inside
    /// the slot claim's transaction - a second connection would neither see the uncommitted Participant resets nor be rolled back with them.
    /// </para>
    /// </remarks>
    /// <param name="turnQueueId">The lane the Turn is being taken in.</param>
    Task<ServiceResult<int>> OpenPlayerTurnAsync(
        AscendantEmpireContext context,
        int turnQueueId,
        int playerId,
        int roundNumber,
        CancellationToken cancelToken = default);

    /// <summary>
    /// Closes whichever Turn is open in the lane, stamping <see cref="PlayerTurn.EndedAt"/>.
    /// </summary>
    /// <remarks>
    /// Fails when nothing is open. Every Turn is opened before its start-of-turn pipeline runs, so a lane with no open Turn at the moment one is ending means
    /// something handed the Turn on without opening a row - and ending it anyway would silently lose that Turn from the record.
    /// </remarks>
    /// <param name="turnQueueId">The lane whose open Turn is being closed.</param>
    Task<ServiceResultBase> ClosePlayerTurnAsync(
        AscendantEmpireContext context,
        int turnQueueId,
        int expectedPlayerId,
        CancellationToken cancelToken = default);

    /// <summary>
    /// Allocates the next <paramref name="count"/> sequence numbers of a Turn, atomically.
    /// </summary>
    /// <remarks>
    /// Fails if the Turn does not exist or has already been closed. A closed Turn is a real refusal rather than an empty allocation: appending a feed row to
    /// a Turn whose intelligence fold has already run produces intelligence that silently never arrives.
    /// </remarks>
    /// <param name="playerTurnId">The Turn to allocate from.</param>
    /// <param name="count">How many consecutive numbers to reserve. Must be greater than zero.</param>
    Task<ServiceResult<TurnSequenceBlock>> AllocateSequenceAsync(
        AscendantEmpireContext context,
        int playerTurnId,
        int count = 1,
        CancellationToken cancelToken = default);

    /// <summary>
    /// Allocates sequence numbers on a context of its own, for callers not already inside a transaction.
    /// </summary>
    /// <inheritdoc cref="AllocateSequenceAsync(AscendantEmpireContext, int, int, CancellationToken)" path="/remarks"/>
    /// <param name="playerTurnId">The Turn to allocate from.</param>
    /// <param name="count">How many consecutive numbers to reserve. Must be greater than zero.</param>
    Task<ServiceResult<TurnSequenceBlock>> AllocateSequenceAsync(int playerTurnId, int count = 1, CancellationToken cancelToken = default);
}
