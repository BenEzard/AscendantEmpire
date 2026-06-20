using AscendantEmpire.Domain.Enums.Maps;

namespace AscendantEmpire.BackendContracts.UnitDesigns.BulkUpsertUnitComponents;

/// <summary>
/// Declares that a weapon can attack targets in a given Domain, and by how much its damage is scaled.
/// </summary>
/// <remarks>
/// The presence of a row gates targeting: a weapon can only attack a Domain it has a modifier for.
/// </remarks>
public record UnitComponentWeaponDomainModifierDto
{
    /// <summary>
    /// The target Domain this weapon can engage.
    /// </summary>
    public required DomainType Domain { get; init; }

    /// <summary>
    /// Raw fixed-point multiplier (Scale 1,000; 1,000 = 100%) applied to the weapon's attack profile
    /// when striking targets in this Domain.
    /// </summary>
    public long DamageMultiplier { get; init; }
}
