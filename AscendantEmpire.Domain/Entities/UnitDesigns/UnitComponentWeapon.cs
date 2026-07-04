namespace AscendantEmpire.Domain.Entities.UnitDesigns;

/// <summary>
/// Weapon-specific properties for a <see cref="UnitComponent"/> whose type is <c>Weapon</c>.
/// </summary>
/// <remarks>
/// One-to-one with <see cref="UnitComponent"/> (shares its primary key), mirroring
/// <see cref="UnitComponentBody"/>. A weapon is purely offensive: it deals <see cref="Attack"/>
/// damage at a given <see cref="Range"/>, and can only engage the target Domains listed in
/// <see cref="DomainModifiers"/> (each with its own damage multiplier). All damage mitigation
/// lives on armour and bodies, not here.
/// </remarks>
public class UnitComponentWeapon
{
    /// <summary>
    /// One-to-one with <see cref="UnitComponent"/>; also the primary key.
    /// </summary>
    public int UnitComponentId { get; set; }

    /// <summary>
    /// The damage this weapon deals, split across the five damage types.
    /// </summary>
    public DamageProfile Attack { get; set; } = new();

    /// <summary>
    /// The closest distance (in tiles) the weapon can engage. Together with <see cref="MaximumRange"/>
    /// this forms a range band: a target is in range when its distance is within
    /// <see cref="MinimumRange"/>..<see cref="MaximumRange"/> inclusive. Indirect-fire weapons (e.g.
    /// artillery) use a minimum greater than 1 to create a close-up dead zone they cannot hit.
    /// </summary>
    public int MinimumRange { get; set; }

    /// <summary>
    /// The furthest distance (in tiles) the weapon can engage. A value of 1 (with a minimum of 0 or 1)
    /// is a melee weapon; greater values are ranged.
    /// </summary>
    public int MaximumRange { get; set; }

    /// <summary>
    /// Whether this weapon is wielded two at a time (e.g. dual pistols). Purely descriptive for now —
    /// it does not yet affect attack calculation.
    /// </summary>
    public bool DualWield { get; set; }

    public UnitComponent UnitComponent { get; set; } = null!;

    /// <summary>
    /// The target Domains this weapon can engage, each with a per-Domain damage multiplier.
    /// A Domain with no row here cannot be attacked by this weapon.
    /// </summary>
    public ICollection<UnitComponentWeaponDomainModifier> DomainModifiers { get; set; } = [];
}
