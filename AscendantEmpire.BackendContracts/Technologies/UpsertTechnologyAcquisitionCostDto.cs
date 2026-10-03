namespace AscendantEmpire.BackendContracts.Technologies;

/// <summary>
/// One Asset, and how much of it, a Technology costs to unlock.
/// </summary>
/// <remarks>
/// The Asset is named by code rather than by id, because the seed is authored before any id exists and a hand-written id would break the moment the catalogue
/// is reseeded into a fresh database.
/// </remarks>
public record UpsertTechnologyAcquisitionCostDto
{
    /// <summary>
    /// The code of the Asset to be paid, e.g. "ConstructionMaterials.Stone.Bricks.SandstoneBlock".
    /// </summary>
    /// <remarks>Must resolve to an existing Asset, so the Assets catalogue has to be seeded first.</remarks>
    public required string AssetCode { get; init; }

    /// <summary>
    /// How much of the Asset is required. Must be greater than zero.
    /// </summary>
    /// <remarks>A free Technology carries no cost rows at all rather than a row of nothing.</remarks>
    public required int Quantity { get; init; }
}
