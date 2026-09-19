using AscendantEmpire.FrontendContracts.MapResources.AddOrUpdateMapResourceType;

namespace AscendantEmpire.FrontendContracts.MapResources.PutMapResourceCatalogue;

/// <summary>
/// The MapResource catalogue as the MapResource Type Manager saves it: categories and types, each added or updated by its code.
/// </summary>
/// <remarks>
/// Saved together so a new category and the types in it arrive in one request, and are written in one transaction. Nothing absent is deleted: deleting a type or a
/// category is its own request.
/// </remarks>
public interface IPutMapResourceCatalogueDto
{
    IReadOnlyCollection<IAddOrUpdateMapResourceCategoryDto> Categories { get; }

    IReadOnlyCollection<IAddOrUpdateMapResourceTypeDto> Types { get; }
}

/// <summary>
/// A MapResourceCategory as the MapResource Type Manager adds or updates it.
/// </summary>
public interface IAddOrUpdateMapResourceCategoryDto
{
    /// <summary>
    /// Unique, stable key, e.g. "Food". What an update matches on, and what a type names its category by.
    /// </summary>
    string Code { get; }

    /// <summary>
    /// The name shown in the map editor.
    /// </summary>
    string DisplayName { get; }

    /// <summary>
    /// Where the category comes in the map editor's list, lowest first.
    /// </summary>
    int SortOrder { get; }
}
