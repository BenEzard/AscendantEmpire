namespace AscendantEmpire.FrontendContracts.Technologies.GetTechnologies;

/// <summary>
/// One Asset, and how much of it, a technology costs to unlock.
/// </summary>
public interface ICatalogueAcquisitionCost
{
    int AssetId { get; }

    string AssetCode { get; }

    string AssetDisplayName { get; }

    /// <summary>
    /// The Asset's catalogue image, as a root-relative URL. Empty where the Asset has no icon.
    /// </summary>
    string AssetIconPath { get; }

    int Quantity { get; }
}
