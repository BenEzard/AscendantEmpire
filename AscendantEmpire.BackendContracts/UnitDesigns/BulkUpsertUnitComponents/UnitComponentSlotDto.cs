namespace AscendantEmpire.BackendContracts.UnitDesigns.BulkUpsertUnitComponents;

/// <summary>
/// A Template Slot defined by a component — a place where another Unit Component can be plugged in.
/// </summary>
public record UnitComponentSlotDto
{
    /// <summary>
    /// Optional label shown for the slot.
    /// </summary>
    public string? DisplayName { get; init; }

    /// <summary>
    /// Whether the slot is shown on the UI.
    /// </summary>
    public bool IsVisible { get; init; }

    /// <summary>
    /// Whether the slot is locked (cannot be changed by the player).
    /// </summary>
    public bool IsLocked { get; init; }

    /// <summary>
    /// X position where the slot sits on the UI.
    /// </summary>
    public int UIXPosition { get; init; }

    /// <summary>
    /// Y position where the slot sits on the UI.
    /// </summary>
    public int UIYPosition { get; init; }

    /// <summary>
    /// Acceptance rules that decide which components may be fitted (1 or more).
    /// </summary>
    public IReadOnlyCollection<UnitComponentSlotRuleDto> Rules { get; init; } = [];
}
