using System.ComponentModel.DataAnnotations;

namespace AscendantEmpire.Domain.Entities.UnitDesigns;

/// <summary>
/// A Template Slot defined by a body — a place where another Unit Component can be plugged in.
/// </summary>
/// <remarks>
/// Only body components expose slots; a Unit Template is built around a body and every other component is
/// fitted into one of its slots.
/// </remarks>
public class UnitComponentSlot
{
    public const int DISPLAY_NAME_MAX_LENGTH = 100;

    public int Id { get; set; }

    /// <summary>
    /// The body (via <see cref="UnitComponentBody.UnitComponentId"/>) that owns this slot.
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
    /// Normalized X offset of the slot from the owning component's image center.
    /// </summary>
    /// <remarks>
    /// 0 is the image center; -1 / +1 are the left / right edges (i.e. relative to the image half-width).
    /// Values may fall outside -1..1 for slots that sit beyond the image bounds.
    /// </remarks>
    public decimal UIXPosition { get; set; }

    /// <summary>
    /// Normalized Y offset of the slot from the owning component's image center.
    /// </summary>
    /// <remarks>
    /// 0 is the image center; -1 / +1 are the top / bottom edges (i.e. relative to the image half-height).
    /// Values may fall outside -1..1 for slots that sit beyond the image bounds.
    /// </remarks>
    public decimal UIYPosition { get; set; }

    /// <summary>
    /// The body that owns (exposes) this slot.
    /// </summary>
    public UnitComponentBody Body { get; set; } = null!;

    /// <summary>
    /// Acceptance rules that decide which components may be fitted (1 or more).
    /// </summary>
    public ICollection<UnitComponentSlotRule> Rules { get; set; } = [];
}
