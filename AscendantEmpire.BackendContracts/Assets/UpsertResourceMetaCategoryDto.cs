namespace AscendantEmpire.BackendContracts.Assets;

public record UpsertResourceMetaCategoryDto
{
    /// <summary>
    /// Display name, e.g. "Construction Materials".
    /// Code is derived automatically: non-alpha characters removed and ProperCase applied.
    /// SortOrder is calculated automatically in steps of 10 based on the order provided.
    /// </summary>
    public required string Name { get; init; }

    /// <summary>
    /// Optional path to an icon file.
    /// </summary>
    public string? IconPath { get; init; }

    /// <summary>
    /// Child categories belonging to this meta-category.
    /// </summary>
    public IReadOnlyCollection<UpsertResourceCategoryDto> Categories { get; init; } = [];
}
