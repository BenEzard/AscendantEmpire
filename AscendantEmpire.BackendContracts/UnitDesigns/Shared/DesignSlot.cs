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

    /// <summary>
    /// Whether the slot is locked for the recipient. On the player-scoped read endpoints this is the
    /// *effective* per-player value (design-locked and not yet unlocked by that player); on the dev
    /// god-view, which has no player, it is the raw design-time flag.
    /// </summary>
    public required bool IsLocked { get; init; }
    /// <summary>Normalized X offset from the owning component's image center (0 = center, ±1 = edges).</summary>
    public required decimal UIXPosition { get; init; }

    /// <summary>Normalized Y offset from the owning component's image center (0 = center, ±1 = edges).</summary>
    public required decimal UIYPosition { get; init; }
    public required IReadOnlyCollection<DesignSlotRule> Rules { get; init; }
}
