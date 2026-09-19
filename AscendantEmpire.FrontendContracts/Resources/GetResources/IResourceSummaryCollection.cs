namespace AscendantEmpire.FrontendContracts.Resources.GetResources;

/// <summary>
/// Every Resource, for pickers such as the MapResource Type Manager's yields.
/// </summary>
public interface IResourceSummaryCollection
{
    IReadOnlyCollection<IResourceSummary> Resources { get; }
}

/// <summary>
/// Enough of a Resource to choose it from a list.
/// </summary>
public interface IResourceSummary
{
    int Id { get; }

    /// <summary>
    /// The Resource's unique code, e.g. "Food.Forage.Berries".
    /// </summary>
    string Code { get; }

    string DisplayName { get; }

    /// <summary>
    /// The display name of the meta category the Resource belongs to, e.g. "Food", for grouping the list.
    /// </summary>
    string MetaCategoryDisplayName { get; }
}
