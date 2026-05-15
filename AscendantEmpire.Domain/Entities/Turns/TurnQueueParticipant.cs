using AscendantEmpire.Domain.Entities.Games;
using AscendantEmpire.Domain.Enums.Turns;

using System.ComponentModel.DataAnnotations;

namespace AscendantEmpire.Domain.Entities.Turns;

/// <summary>
/// Represents a Player's participation in a specific turn queue.
/// </summary>
/// <remarks>
/// Answers the question "how does this Player participate in the turn queue for this lane?"
/// It is intentionally separated from BattleParticipant, which records a Player's participation in a Battle.
/// </remarks>
public class TurnQueueParticipant
{
    public int Id { get; set; }
    public int TurnQueueId { get; set; }
    public int PlayerId { get; set; }

    public int TurnOrder { get; set; }

    public TurnQueueParticipantStatus Status { get; set; }

    public bool IsActiveInLane { get; set; }

    public int? LastCompletedRoundNumber { get; set; }

    public DateTimeOffset? LastTurnStartedAt { get; set; }
    public DateTimeOffset? LastTurnEndedAt { get; set; }

    [Timestamp]
    public byte[] RowVersion { get; set; } = null!;

    public TurnQueue TurnQueue { get; set; } = null!;
    public Player Player { get; set; } = null!;
}