namespace AscendantEmpire.BackendContracts.UnitDesigns.GetUnlockedBodies;

/// <summary>
/// A slot exposed by a body component: where it sits on the UI, its display/lock state, and the
/// acceptance rules that decide which components may be fitted into it.
/// </summary>
public record DesignBodySlot
{
    public required int Id { get; init; }
    public string? DisplayName { get; init; }
    public required bool IsVisible { get; init; }
    public required bool IsLocked { get; init; }
    public required int UIXPosition { get; init; }
    public required int UIYPosition { get; init; }
    public required IReadOnlyCollection<DesignSlotRule> Rules { get; init; }
}
