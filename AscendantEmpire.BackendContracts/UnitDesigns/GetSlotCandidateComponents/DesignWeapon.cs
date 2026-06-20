namespace AscendantEmpire.BackendContracts.UnitDesigns.GetSlotCandidateComponents;

/// <summary>
/// Weapon stats for a candidate component: its attack profile, range band, and the target Domains it
/// can engage.
/// </summary>
public record DesignWeapon
{
    public required DesignDamageProfile Attack { get; init; }
    public required int MinimumRange { get; init; }
    public required int MaximumRange { get; init; }
    public required IReadOnlyCollection<DesignWeaponDomainModifier> DomainModifiers { get; init; }
}
