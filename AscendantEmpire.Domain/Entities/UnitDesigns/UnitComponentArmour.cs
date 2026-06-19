namespace AscendantEmpire.Domain.Entities.UnitDesigns;

/// <summary>
/// Armour-specific properties for a <see cref="UnitComponent"/> whose type is <c>Armour</c>.
/// </summary>
/// <remarks>
/// One-to-one with <see cref="UnitComponent"/> (shares its primary key), mirroring
/// <see cref="UnitComponentBody"/>. Armour has no range; its <see cref="Resistance"/> values are
/// flat reduction subtracted from incoming damage of the matching type by the combat layer.
/// </remarks>
public class UnitComponentArmour
{
    /// <summary>
    /// One-to-one with <see cref="UnitComponent"/>; also the primary key.
    /// </summary>
    public int UnitComponentId { get; set; }

    /// <summary>
    /// Flat damage reduction per damage type. Shares the same raw fixed-point units as weapon attack.
    /// </summary>
    public DamageProfile Resistance { get; set; } = new();

    public UnitComponent UnitComponent { get; set; } = null!;
}
