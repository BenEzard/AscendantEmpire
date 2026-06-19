using System.ComponentModel.DataAnnotations;

namespace AscendantEmpire.Domain.Entities.UnitDesigns;

/// <summary>
/// A Template Slot defined by a body component — a place where another Unit Component can be plugged in.
/// </summary>
public class UnitComponentSlot
{
    public const int DISPLAY_NAME_MAX_LENGTH = 100;

    public int Id { get; set; }

    /// <summary>
    /// The body component (via <see cref="UnitComponentBody.UnitComponentId"/>) that owns this slot.
    /// </summary>
    public int UnitComponentBodyId { get; set; }

    /// <summary>
    /// Optional label shown for the slot.
    /// </summary>
    [MaxLength(DISPLAY_NAME_MAX_LENGTH)]
    public string? DisplayName { get; set; }

    /// <summary>
    /// Whether the slot is shown on the UI.
    /// </summary>
    public bool IsVisible { get; set; }

    /// <summary>
    /// Whether the slot is locked (cannot be changed by the player).
    /// </summary>
    public bool IsLocked { get; set; }

    /// <summary>
    /// X position where the slot sits on the UI.
    /// </summary>
    public int UIXPosition { get; set; }

    /// <summary>
    /// Y position where the slot sits on the UI.
    /// </summary>
    public int UIYPosition { get; set; }

    public UnitComponentBody Body { get; set; } = null!;

    /// <summary>
    /// Acceptance rules that decide which components may be fitted (1 or more).
    /// </summary>
    public ICollection<UnitComponentSlotRule> Rules { get; set; } = [];
}
