namespace AscendantEmpire.BackendContracts.UnitDesigns.Shared;

/// <summary>
/// A slot exposed by a component: where it sits on the UI, its display/lock state, and the acceptance
/// rules that decide which components may be fitted into it. Any component (not just a body) can expose
/// slots, so this shape is shared across the design read endpoints.
/// </summary>
public record DesignSlot
{
    public required int Id { get; init; }
    public string? DisplayName { get; init; }
    public required bool IsVisible { get; init; }
    public required bool IsLocked { get; init; }
    public required int UIXPosition { get; init; }
    public required int UIYPosition { get; init; }
    public required IReadOnlyCollection<DesignSlotRule> Rules { get; init; }
}
