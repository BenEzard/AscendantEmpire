namespace AscendantEmpire.BackendContracts.UnitDesigns.GetSlotCandidateComponents;

/// <summary>
/// Armour stats for a candidate component: flat damage reduction per damage type.
/// </summary>
public record DesignArmour
{
    public required DesignDamageProfile Resistance { get; init; }
}
