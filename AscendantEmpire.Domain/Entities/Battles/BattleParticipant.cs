using AscendantEmpire.Domain.Entities.Games;
using AscendantEmpire.Domain.Enums.Battle;

using System.ComponentModel.DataAnnotations;

namespace AscendantEmpire.Domain.Entities.Battles;

/// <summary>
/// Represents a Player's involvement in a particular Battle.
/// </summary>
/// <remarks>
/// Answers the question "is this Player part of the battle?"
/// It is intentionally separated from TurnQueueParticipant, which records a Player's participation in a specific Queue.
/// </remarks>

public class BattleParticipant
{
    public const int BATTLE_NAME_LENGTH = 250;

    public int BattleId { get; set; }
    public int PlayerId { get; set; }

    /// <summary>
    /// When this Player joined the battle.
    /// </summary>
    public int JoinedAtRoundNumber { get; set; }

    public bool IsStillInBattle { get; set; }

    public int? LeftAtRoundNumber { get; set; }
    public BattleExitReason ExitReason { get; set; }

    [MaxLength(BATTLE_NAME_LENGTH)]
    public string? PlayerBattleName { get; set; }

    /// <summary>
    /// Were this Player's Units supplied at the end of last turn?
    /// </summary>
    public bool WasSupplied { get; set; }

    /// <summary>
    /// Is this Player's supply lines still intact now?
    /// </summary>
    public bool IsSupplied { get; set; }

    [Timestamp]
    public byte[] RowVersion { get; set; } = null!;

    public Battle Battle { get; set; } = null!;
    public Player Player { get; set; } = null!;
}
