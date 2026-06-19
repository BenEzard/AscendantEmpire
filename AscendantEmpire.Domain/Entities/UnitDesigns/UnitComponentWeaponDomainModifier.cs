using AscendantEmpire.Domain.Enums.Maps;

namespace AscendantEmpire.Domain.Entities.UnitDesigns;

/// <summary>
/// Declares that a weapon can attack targets in a given Domain, and by how much its damage is scaled.
/// </summary>
/// <remarks>
/// <para>
/// The presence of a row gates targeting: a weapon can only attack a Domain it has a modifier for.
/// For example a machine gun would have a Land row at full effect and an Air row at reduced effect,
/// but no Water row at all.
/// </para>
/// <para>
/// <see cref="DamageMultiplier"/> is a raw fixed-point percentage using the Scale 1,000 convention
/// of <c>ScaledPercent</c> (1,000 = 100%, 500 = 50%). The combat layer applies it to the weapon's
/// whole attack profile via <c>ScaledAmount.CalculateBonus(ScaledPercent)</c>.
/// </para>
/// </remarks>
public class UnitComponentWeaponDomainModifier
{
    public int Id { get; set; }

    /// <summary>
    /// The weapon (via <see cref="UnitComponentWeapon.UnitComponentId"/>) that owns this modifier.
    /// </summary>
    public int UnitComponentWeaponId { get; set; }

    /// <summary>
    /// The target Domain this weapon can engage.
    /// </summary>
    public DomainType Domain { get; set; }

    /// <summary>
    /// Raw fixed-point multiplier (Scale 1,000; interpreted as <c>ScaledPercent</c>) applied to the
    /// weapon's whole attack profile when striking targets in this Domain.
    /// </summary>
    public long DamageMultiplier { get; set; }

    public UnitComponentWeapon Weapon { get; set; } = null!;
}
