using AscendantEmpire.Domain.Enums.Resources;

namespace AscendantEmpire.BackendContracts.Resources;

public record UpsertResourceCategoryDto
{
    /// <summary>
    /// Unique code for the Resource Category, e.g. "Stone" (within the <c>ResourceMetaCategory</c>).
    /// </summary>
    /// <remarks>If not supplied, code is derived automatically by normalising DisplayName with non-alpha characters removed and ProperCase applied.</remarks>
    public string? Code { get; init; }

    /// <summary>
    /// Display name, e.g. "Stone".
    /// </summary>
    public required string DisplayName { get; init; }

    /// <summary>
    /// Whether resources in this category are displayed as a grouped ticker list or a flat standalone list.
    /// </summary>
    public required ResourceCategoryType CategoryType { get; init; }

    /// <summary>
    /// Optional sort order for display.
    /// </summary>
    /// <remarks>If not provided, it will be calculated automatically based on the order of the categories in the input list, with gaps for future insertions.</remarks>
    public int? SortOrder { get; init; }

    /// <summary>
    /// Groups of resources within this category. Only used when <see cref="CategoryType"/> is <c>GroupedResources</c>.
    /// </summary>
    public IReadOnlyCollection<UpsertResourceGroupDto> Groups { get; init; } = [];

    /// <summary>
    /// Resources that belong directly to this category. Only used when <see cref="CategoryType"/> is <c>StandaloneResources</c>.
    /// </summary>
    public IReadOnlyCollection<UpsertResourceDto> Resources { get; init; } = [];
}
