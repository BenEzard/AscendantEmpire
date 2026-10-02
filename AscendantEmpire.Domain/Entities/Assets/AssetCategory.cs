using AscendantEmpire.Domain.Enums.Assets;

namespace AscendantEmpire.Domain.Entities.Assets;

/// <summary>
/// Optional middle grouping such as "Stone", "Wood", etc.
/// </summary>
public sealed class AssetCategory
{
    /// <summary>
    /// Codes are hierarchical, so the limit has to hold a full dotted path rather than one segment - "ConstructionMaterials.Stone.Bricks.SandstoneBlock" is already 49.
    /// </summary>
    public const int MAX_CODE_LENGTH = 100;
    public const int MAX_DISPLAY_NAME_LENGTH = 50;

    public int Id { get; set; }

    public int AssetMetaCategoryId { get; set; }

    /// <summary>
    /// Unique code for the Asset Category, e.g. "Stone" (within the <c>AssetMetaCategory</c>).
    /// </summary>
    public required string Code { get; set; } = null!;

    /// <summary>
    /// Denotes the type of Categories this will hold (Standalone or Grouped)
    /// </summary>
    public required AssetCategoryType CategoryType { get; set; }

    /// <summary>
    /// Display name, e.g. "Stone".
    /// </summary>
    public required string DisplayName { get; set; } = null!;

    /// <summary>
    /// Sort order for display.
    /// </summary>
    public required int SortOrder { get; set; }

    /// <summary>
    /// An AssetCategory must belong to an AssetMetaCategory
    /// </summary>
    public AssetMetaCategory MetaCategory { get; set; } = null!;

    /// <summary>
    /// AssetGroups within this category (GroupedAssets categories only).
    /// </summary>
    public ICollection<AssetGroup> AssetGroups { get; set; } = [];

    /// <summary>
    /// Assets that belong directly to this category (StandaloneAssets categories only).
    /// </summary>
    public ICollection<Asset> Assets { get; set; } = [];
}