using AscendantEmpire.Domain.Entities.Battles;
using AscendantEmpire.Domain.Entities.Games;
using AscendantEmpire.Domain.Enums.Turns;

using System.ComponentModel.DataAnnotations;

namespace AscendantEmpire.Domain.Entities.Turns;

/// <summary>
/// An independently progressing turn queue.
/// <list type="bullet">
/// <item>one campaign queue per Game</item>
/// <item>one battle queue per Battle</item>
/// </list>
public class TurnQueue
{
    public int Id { get; set; }
    public int GameId { get; set; }

    public TurnQueueType QueueType { get; set; }

    public int? BattleId { get; set; }

    public int CurrentRoundNumber { get; set; }
    public DateTimeOffset? CurrentTurnStartAt { get; set; }
    public DateTimeOffset? CurrentScheduledTurnEndAt { get; set; }
    public DateTimeOffset? CurrentActualTurnEndAt { get; set; }

    /// <summary>
    /// Set to true when the Campaign or Battle is completed and this Turn Lane is no longer active.
    /// </summary>
    public bool IsCompleted { get; set; }

    public DateTimeOffset CreatedAt { get; set; }

    public DateTimeOffset UpdatedAt { get;set; }

    [Timestamp]
    public byte[] RowVersion { get; set; } = null!;

    public GameIndex Game { get; set; } = null!;
    public Battle? Battle { get; set; }
    public List<TurnQueueParticipant> TurnQueueParticipants { get; set; } = [];
}
