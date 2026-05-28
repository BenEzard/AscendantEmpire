namespace AscendantEmpire.BackendContracts.Assets;

public record UpsertResourceCategoryDto
{
    /// <summary>
    /// Display name, e.g. "Stone".
    /// Code is derived automatically as "{ParentCode}.{NormalisedName}".
    /// SortOrder is calculated automatically in steps of 10 based on the order provided.
    /// </summary>
    public required string Name { get; init; }
}
