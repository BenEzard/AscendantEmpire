namespace AscendantEmpire.FrontendContracts.Assets.GetAssets;

/// <summary>
/// Every Asset, for pickers such as the MapResource Type Manager's yields.
/// </summary>
public interface IAssetSummaryCollection
{
    IReadOnlyCollection<IAssetSummary> Assets { get; }
}

/// <summary>
/// Enough of an Asset to choose it from a list.
/// </summary>
public interface IAssetSummary
{
    int Id { get; }

    /// <summary>
    /// The Asset's unique code, e.g. "Food.Forage.Berries".
    /// </summary>
    string Code { get; }

    string DisplayName { get; }

    /// <summary>
    /// The display name of the meta category the Asset belongs to, e.g. "Food", for grouping the list.
    /// </summary>
    string MetaCategoryDisplayName { get; }
}
