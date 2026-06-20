namespace AscendantEmpire.BackendContracts.UnitDesigns.BulkUpsertUnitComponents;

/// <summary>
/// Armour-specific properties for a component whose type is <c>Armour</c>.
/// </summary>
public record UnitComponentArmourDto
{
    /// <summary>
    /// Flat damage reduction per damage type, subtracted from incoming damage of the matching type.
    /// </summary>
    public required DamageProfileDto Resistance { get; init; }
}
