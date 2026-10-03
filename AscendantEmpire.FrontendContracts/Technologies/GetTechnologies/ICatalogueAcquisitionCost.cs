namespace AscendantEmpire.FrontendContracts.Technologies.GetTechnologies;

/// <summary>
/// One Asset, and how much of it, a technology costs to unlock.
/// </summary>
public interface ICatalogueAcquisitionCost
{
    int AssetId { get; }

    string AssetCode { get; }

    string AssetDisplayName { get; }

    int Quantity { get; }
}
