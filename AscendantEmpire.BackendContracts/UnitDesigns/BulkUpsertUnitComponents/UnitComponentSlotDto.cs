namespace AscendantEmpire.BackendContracts.UnitDesigns.BulkUpsertUnitComponents;

/// <summary>
/// A Template Slot defined by a component — a place where another Unit Component can be plugged in.
/// </summary>
public record UnitComponentSlotDto
{
    /// <summary>
    /// The slot's database id. Populated on reads (e.g. the dev designer's component fetch) so a saved
    /// template's fitted components — keyed by this id — can be matched back onto the right slot; null
    /// on writes, since the bulk-upsert path replaces a component's slots wholesale and never reads it.
    /// </summary>
    public int? Id { get; init; }

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
    /// Normalized X offset of the slot from the owning component's image center (0 = center, ±1 = edges).
    /// </summary>
    public decimal UIXPosition { get; init; }

    /// <summary>
    /// Normalized Y offset of the slot from the owning component's image center (0 = center, ±1 = edges).
    /// </summary>
    public decimal UIYPosition { get; init; }

    /// <summary>
    /// Acceptance rules that decide which components may be fitted (1 or more).
    /// </summary>
    public IReadOnlyCollection<UnitComponentSlotRuleDto> Rules { get; init; } = [];
}
