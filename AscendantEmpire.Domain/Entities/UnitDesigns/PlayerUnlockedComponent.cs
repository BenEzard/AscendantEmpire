using AscendantEmpire.Domain.Entities.Games;

namespace AscendantEmpire.Domain.Entities.UnitDesigns;

/// <summary>
/// Records that a player has unlocked a particular <see cref="UnitComponent"/>.
/// </summary>
/// <remarks>
/// The presence of a row means the component is unlocked for that player. Rows are populated by the
/// (future) tech-advance system. Composite primary key on (PlayerId, UnitComponentId).
/// </remarks>
public class PlayerUnlockedComponent
{
    public int PlayerId { get; set; }
    public int UnitComponentId { get; set; }

    /// <summary>
    /// The turn on which this component became unlocked for the player.
    /// </summary>
    public int UnlockedOnTurn { get; set; }

    /// <summary>
    /// The turn on which this component became obsolete for the player; null while still current.
    /// </summary>
    public int? ObsoleteOnTurn { get; set; }

    public Player Player { get; set; } = null!;
    public UnitComponent UnitComponent { get; set; } = null!;
}
