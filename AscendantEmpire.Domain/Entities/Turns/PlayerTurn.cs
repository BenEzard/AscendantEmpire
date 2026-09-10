using AscendantEmpire.Domain.Entities.Games;

namespace AscendantEmpire.Domain.Entities.Turns;

/// <summary>
/// One Player's single Turn in one Turn Queue: when it opened, when it closed, and the counter that orders everything they did during it.
/// </summary>
/// <remarks>
/// <b>Why the row exists at all.</b> <see cref="TurnQueue"/> already says whose Turn it is <i>now</i>, and <see cref="TurnQueueParticipant"/> remembers when
/// that Player last started and finished. Neither is an identity another row can point at, so nothing could say "this happened during that Turn" - which is
/// what the intelligence feeds need, and what <c>PlayerCampaignMap.FirstSeenOnTurnId</c> had been faking with a literal <c>1</c>.
/// <para>
/// Hangs off <see cref="TurnQueue"/> rather than off a Game, so it is lane-agnostic by construction: a Battle lane gets Turn rows on exactly the same terms
/// as the Campaign lane, with no schema change, the day something opens them. Today only the campaign turn pipeline opens them.
/// </para>
/// <para>
/// <b>The counter.</b> <see cref="LastAllocatedSequence"/> is the high-water mark of the sequence numbers handed out during this Turn. Movement, structure
/// and combat records all draw from it, which is what lets three separate feeds interleave into one total order across the Turn - "rode past the village,
/// then razed it" is different intelligence from the reverse, and only a shared counter can tell them apart. See
/// <c>docs/design/intelligence-and-sightings.md</c>.
/// </para>
/// <para>
/// <b>No RowVersion,</b> unlike both sibling tables in this schema. Every write to this row is a single atomic SQL statement and none of them consults a
/// concurrency token, so carrying the column would advertise optimistic concurrency on a counter that must never be lost to a retry.
/// </para>
/// </remarks>
public class PlayerTurn
{
    public int Id { get; set; }

    /// <summary>
    /// The lane this Turn belongs to - Campaign or Battle.
    /// </summary>
    /// <remarks>The Game is reached through the Queue, which is why there is no GameId column of its own.</remarks>
    public int TurnQueueId { get; set; }

    public int PlayerId { get; set; }

    /// <summary>
    /// The Round the Turn was taken in, copied from <see cref="TurnQueue.CurrentRoundNumber"/> as the Turn opened.
    /// </summary>
    /// <remarks>Copied rather than joined for a reason: the Queue's Round number moves on, and a Turn has to keep saying which Round it was, for ever.</remarks>
    public int RoundNumber { get; set; }

    public DateTimeOffset StartedAt { get; set; }

    /// <summary>
    /// Null while the Turn is open. Set once, when the Turn ends.
    /// </summary>
    /// <remarks>This is the whole state machine - there is deliberately no status enum, because "open" and "ended" are the only two states a Turn has.</remarks>
    public DateTimeOffset? EndedAt { get; set; }

    /// <summary>
    /// The highest sequence number handed out during this Turn; 0 when nothing has been recorded yet.
    /// </summary>
    /// <remarks>
    /// A high-water mark, not a "next" value: the first allocation returns 1, and a block of <c>n</c> returns the inclusive range ending here. Allocation
    /// goes through <c>IPlayerTurnService.AllocateSequenceAsync</c>, which increments and reads back in one atomic statement.
    /// <para>
    /// <b>Gaps are legal.</b>
    /// </para>
    /// </remarks>
    public int LastAllocatedSequence { get; set; }

    public TurnQueue TurnQueue { get; set; } = null!;
    public Player Player { get; set; } = null!;
}
