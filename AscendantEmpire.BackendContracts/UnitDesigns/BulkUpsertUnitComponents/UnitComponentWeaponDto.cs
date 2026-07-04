namespace AscendantEmpire.BackendContracts.UnitDesigns.BulkUpsertUnitComponents;

/// <summary>
/// Weapon-specific properties for a component whose type is <c>Weapon</c>.
/// </summary>
public record UnitComponentWeaponDto
{
    /// <summary>
    /// The damage this weapon deals, split across the five damage types.
    /// </summary>
    public required DamageProfileDto Attack { get; init; }

    /// <summary>
    /// The closest distance (in tiles) the weapon can engage.
    /// </summary>
    public int MinimumRange { get; init; }

    /// <summary>
    /// The furthest distance (in tiles) the weapon can engage. 1 (with a low minimum) is melee.
    /// </summary>
    public int MaximumRange { get; init; }

    /// <summary>
    /// Whether this weapon is wielded two at a time (e.g. dual pistols).
    /// </summary>
    public bool DualWield { get; init; }

    /// <summary>
    /// The target Domains this weapon can engage, each with a per-Domain damage multiplier.
    /// </summary>
    public IReadOnlyCollection<UnitComponentWeaponDomainModifierDto> DomainModifiers { get; init; } = [];
}
