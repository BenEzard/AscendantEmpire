namespace AscendantEmpire.BackendContracts.Technologies.GetTechnologies;

/// <summary>
/// One Asset, and how much of it, a technology costs to unlock.
/// </summary>
/// <remarks>
/// The Asset is named as well as identified, because a client cannot resolve an Asset id without fetching the whole Asset catalogue alongside this one.
/// </remarks>
public record CatalogueAcquisitionCost
{
    public required int AssetId { get; init; }

    public required string AssetCode { get; init; }

    public required string AssetDisplayName { get; init; }

    public required int Quantity { get; init; }
}
