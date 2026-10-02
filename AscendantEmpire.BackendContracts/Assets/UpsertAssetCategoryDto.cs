using AscendantEmpire.Domain.Enums.Assets;

namespace AscendantEmpire.BackendContracts.Assets;

public record UpsertAssetCategoryDto
{
    /// <summary>
    /// Unique code for the Asset Category, e.g. "Stone" (within the <c>AssetMetaCategory</c>).
    /// </summary>
    /// <remarks>If not supplied, code is derived automatically by normalising DisplayName with non-alpha characters removed and ProperCase applied.</remarks>
    public string? Code { get; init; }

    /// <summary>
    /// Display name, e.g. "Stone".
    /// </summary>
    public required string DisplayName { get; init; }

    /// <summary>
    /// Whether assets in this category are displayed as a grouped ticker list or a flat standalone list.
    /// </summary>
    public required AssetCategoryType CategoryType { get; init; }

    /// <summary>
    /// Optional sort order for display.
    /// </summary>
    /// <remarks>If not provided, it will be calculated automatically based on the order of the categories in the input list, with gaps for future insertions.</remarks>
    public int? SortOrder { get; init; }

    /// <summary>
    /// Groups of assets within this category. Only used when <see cref="CategoryType"/> is <c>GroupedAssets</c>.
    /// </summary>
    public IReadOnlyCollection<UpsertAssetGroupDto> Groups { get; init; } = [];

    /// <summary>
    /// Assets that belong directly to this category. Only used when <see cref="CategoryType"/> is <c>StandaloneAssets</c>.
    /// </summary>
    public IReadOnlyCollection<UpsertAssetDto> Assets { get; init; } = [];
}
