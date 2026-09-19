using AscendantEmpire.FrontendContracts.MapResources.AddOrUpdateMapResourceType;

namespace AscendantEmpire.FrontendContracts.MapResources.GetMapResourceCatalogue;

/// <summary>
/// The whole MapResource catalogue, grouped the way the map editor lists it: categories in order, each with its types in order.
/// </summary>
public interface IMapResourceCatalogue
{
    IReadOnlyCollection<IMapResourceCategory> Categories { get; }
}

/// <summary>
/// A MapResourceCategory and the types in it.
/// </summary>
public interface IMapResourceCategory
{
    int Id { get; }

    string Code { get; }

    string DisplayName { get; }

    int SortOrder { get; }

    /// <summary>
    /// The types in the category, by <c>SortOrder</c> and then name.
    /// </summary>
    IReadOnlyCollection<IMapResourceTypeDetail> Types { get; }
}

/// <summary>
/// A MapResourceType with everything it owns, in exactly the shape it is saved in, plus its id.
/// </summary>
/// <remarks>
/// The same shape as <see cref="IAddOrUpdateMapResourceTypeDto"/> so the manager can edit what it loaded and send it straight back.
/// </remarks>
public interface IMapResourceTypeDetail : IAddOrUpdateMapResourceTypeDto
{
    int Id { get; }
}
