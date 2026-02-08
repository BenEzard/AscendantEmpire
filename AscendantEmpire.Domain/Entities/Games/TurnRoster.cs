using AscendantEmpire.Domain.Enums.Games;

using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace AscendantEmpire.Domain.Entities.Games;

/// <summary>
/// Represents a roster entry for a Player's turn within a Game.
/// </summary>
/// <remarks>Each instance of this class corresponds to a single player's turn in a specific game Round. 
/// Covers both the Campaign Map Turns and Battle Map Turns (by virtue of if the BattleId is populated.</remarks>
public class TurnRoster
{
    public const int PLAYER_BATTLE_NAME_MAX_LENGTH = 100;

    /// <summary>
    /// Unique Identifier for the Turn Roster entry.
    /// </summary>
    public int Id { get; set; }
    public required int GameId { get; set; }
    public required int PlayerId { get; set; }

    /// <summary>
    /// The order in which the Player takes their turn within the Round; either for the Campaign or Battles.
    /// </summary>
    public required int TurnOrder { get; set; }
    
    public int? BattleId { get; set; }

    /// <summary>
    /// Is this Player still in the Battle?
    /// </summary>
    /// <remarks>A Player can still be active in the Campaign Map, but once they lose all their Units in a *this* Battle, then they are no longer active in the Battle
    /// (unless they bring in reinforcements).</remarks>
    public bool? PlayerStillInBattle { get; set; }

    /// <summary>
    /// A Player-defined name for the Battle.
    /// </summary>
    /// <remarks>Only populated when </remarks>
    [MaxLength(PLAYER_BATTLE_NAME_MAX_LENGTH)]
    public string? PlayerBattleName { get; set; }

    public required TurnStatus Status { get; set; }

    /// <summary>
    /// The time the Player started their Turn.
    /// </summary>
    public DateTimeOffset? TurnStartTime { get; set; }

    /// <summary>
    /// The time the Player ended their Turn; whether manually or done automatically (e.g. Timeout).
    /// </summary>
    public DateTimeOffset? TurnEndTime { get; set; }

    /// <summary>
    /// When the Player's Turn is scheduled to end (if not Completed manually beforehand).
    /// </summary>
    public DateTimeOffset? ScheduledTurnEndTime { get; set; }

    /// <summary>
    /// Round Number (starting at 1) within the Game that this Turn belongs to.
    /// </summary>
    public required int RoundNumber { get; set; }

    public required DateTimeOffset CreatedAt { get; set; }

    [ForeignKey("GameId")]
    public GameIndex Game { get; set; } = null!;
    
    [ForeignKey("PlayerId")]
    public Player Player { get; set; } = null!;
}
