namespace AscendantEmpire.BackendContracts.UnitDesigns.BulkUpsertUnitComponents;

/// <summary>
/// One kind of ammunition a weapon accepts.
/// </summary>
/// <remarks>
/// The Asset is named by code rather than by id, so a weapon can be authored, exported and seeded without knowing database identities. The code is resolved
/// against the Asset catalogue at save, and one that resolves to nothing fails the payload. An attack spends one unit of it, so there is no rate to carry.
/// </remarks>
public record UnitComponentWeaponAmmunitionDto
{
    public required string AssetCode { get; init; }
}
