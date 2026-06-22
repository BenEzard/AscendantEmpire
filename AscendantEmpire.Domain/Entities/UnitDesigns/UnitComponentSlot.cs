using System.ComponentModel.DataAnnotations;

namespace AscendantEmpire.Domain.Entities.UnitDesigns;

/// <summary>
/// A Template Slot defined by a component — a place where another Unit Component can be plugged in.
/// </summary>
/// <remarks>
/// Any component (not just bodies) may expose slots, so a fitted component can carry its own slots —
/// e.g. a weapon mount whose slots accept a gun and a targeting module.
/// </remarks>
public class UnitComponentSlot
{
    public const int DISPLAY_NAME_MAX_LENGTH = 100;

    public int Id { get; set; }

    /// <summary>
    /// The component (via <see cref="UnitComponent.Id"/>) that owns this slot.
    /// </summary>
    public int UnitComponentId { get; set; }

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

    /// <summary>
    /// The component that owns (exposes) this slot.
    /// </summary>
    public UnitComponent OwnerComponent { get; set; } = null!;

    /// <summary>
    /// Acceptance rules that decide which components may be fitted (1 or more).
    /// </summary>
    public ICollection<UnitComponentSlotRule> Rules { get; set; } = [];
}
