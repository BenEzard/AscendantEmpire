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
/// A MapResourceType with everything it owns, in the shape it is saved in, plus the two values the manager reads but never sends: its id and its position in its category.
/// </summary>
/// <remarks>
/// Built on <see cref="IAddOrUpdateMapResourceTypeDto"/> so the manager can edit what it loaded and send it straight back, dropping only the two read-only values.
/// </remarks>
public interface IMapResourceTypeDetail : IAddOrUpdateMapResourceTypeDto
{
    int Id { get; }

    /// <summary>
    /// Where the type comes within its category, lowest first. Read-only: it is set when the type is created and changed only in the database by hand.
    /// </summary>
    int SortOrder { get; }
}
