using AscendantEmpire.Domain.Entities.Games;

namespace AscendantEmpire.Domain.Entities.UnitDesigns;

/// <summary>
/// Records that a player has unlocked a particular <see cref="UnitComponentSlot"/>.
/// </summary>
/// <remarks>
/// A slot whose <see cref="UnitComponentSlot.IsLocked"/> flag is set starts locked for every player; the
/// presence of a row here opens that slot for this one player. Slots that are not <c>IsLocked</c> never
/// need a row. Rows are written by in-game logic (e.g. the future tech-advance system), not by the player
/// directly. Composite primary key on (PlayerId, UnitComponentSlotId).
/// </remarks>
public class PlayerUnlockedSlot
{
    public int PlayerId { get; set; }
    public int UnitComponentSlotId { get; set; }

    /// <summary>
    /// The turn on which this slot became unlocked for the player.
    /// </summary>
    public int UnlockedOnTurn { get; set; }

    /// <summary>
    /// The turn on which this unlock became obsolete for the player; null while still current.
    /// </summary>
    public int? ObsoleteOnTurn { get; set; }

    public Player Player { get; set; } = null!;
    public UnitComponentSlot Slot { get; set; } = null!;
}
