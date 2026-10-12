namespace AscendantEmpire.BackendContracts.Technologies.GetTechnologies;

/// <summary>
/// One Asset, and how much of it, a technology costs to unlock.
/// </summary>
/// <remarks>
/// The Asset is named, iconned and identified, because a client cannot resolve an Asset id without fetching the whole Asset catalogue alongside this one.
/// </remarks>
public record CatalogueAcquisitionCost
{
    public required int AssetId { get; init; }

    public required string AssetCode { get; init; }

    public required string AssetDisplayName { get; init; }

    /// <summary>
    /// The Asset's catalogue image, as a root-relative URL. Empty where the Asset has no icon.
    /// </summary>
    /// <remarks>Carried so a cost can be shown as the Asset's own icon rather than as its name in text.</remarks>
    public required string AssetIconPath { get; init; }

    public required int Quantity { get; init; }
}
