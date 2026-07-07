namespace AscendantEmpire.BackendContracts.UnitDesigns.Shared;

/// <summary>
/// A slot exposed by a body: where it sits on the UI, its display state, and the acceptance rules that
/// decide which components may be fitted into it. Shared across the design read endpoints.
/// </summary>
public record DesignSlot
{
    public required int Id { get; init; }
    public string? DisplayName { get; init; }
    public required bool IsVisible { get; init; }

    /// <summary>Normalized X offset from the owning component's image center (0 = center, ±1 = edges).</summary>
    public required decimal UIXPosition { get; init; }

    /// <summary>Normalized Y offset from the owning component's image center (0 = center, ±1 = edges).</summary>
    public required decimal UIYPosition { get; init; }
    public required IReadOnlyCollection<DesignSlotRule> Rules { get; init; }
}
