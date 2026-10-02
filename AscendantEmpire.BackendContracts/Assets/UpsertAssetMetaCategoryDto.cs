namespace AscendantEmpire.BackendContracts.Assets;

public record UpsertAssetMetaCategoryDto
{
    /// <summary>
    /// Unique code for the asset meta-category.
    /// </summary>
    /// <remarks>If not supplied, code is derived automatically from the <c>Name</c> by removing non-alpha characters and applying ProperCase.</remarks>
    public string? Code { get; init; }

    /// <summary>
    /// Display name, e.g. "Construction Materials".
    /// </summary>
    public required string DisplayName { get; init; }

    /// <summary>
    /// Path to an icon file.
    /// </summary>
    /// <remarks>Required if the <c>AssetMetaCategory</c> is being created.</remarks>
    public string? IconPath { get; init; }

    /// <summary>
    /// Optional sort order for display. 
    /// </summary>
    /// <remarks>If not provided, it will be calculated automatically based on the order of the meta-categories in the input list, with gaps for future insertions.</remarks>
    public int? SortOrder { get; init; }

    /// <summary>
    /// An AssetMetaCategory contains 1 or more AssetCategories.
    /// </summary>
    public required IReadOnlyCollection<UpsertAssetCategoryDto> Categories { get; init; } = [];
}
