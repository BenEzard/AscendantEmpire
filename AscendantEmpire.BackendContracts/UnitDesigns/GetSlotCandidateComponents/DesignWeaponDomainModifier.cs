using AscendantEmpire.Domain.Enums.Maps;

namespace AscendantEmpire.BackendContracts.UnitDesigns.GetSlotCandidateComponents;

/// <summary>
/// A target Domain a weapon can engage, with its per-Domain damage multiplier (fixed-point, Scale 1,000).
/// </summary>
public record DesignWeaponDomainModifier
{
    public required DomainType Domain { get; init; }
    public required long DamageMultiplier { get; init; }
}
