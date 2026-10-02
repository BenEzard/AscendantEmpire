namespace AscendantEmpire.Domain.Entities.Assets;

/// <summary>
/// Top-level grouping of Assets e.g. "Construction Materials", "Food", "Industrial Inputs", etc.
/// </summary>
public sealed class AssetMetaCategory
{
    /// <summary>
    /// Codes are hierarchical, so the limit has to hold a full dotted path rather than one segment - "ConstructionMaterials.Stone.Bricks.SandstoneBlock" is already 49.
    /// </summary>
    public const int MAX_CODE_LENGTH = 100;
    public const int MAX_DISPLAY_NAME_LENGTH = 50;
    public const int MAX_ICON_PATH_LENGTH = 500;

    public int Id { get; set; }

    /// <summary>
    /// Unique code for the asset meta-category.
    /// </summary>
    public required string Code { get; set; }

    /// <summary>
    /// Display name, e.g. "Construction Materials".
    /// </summary>
    public required string DisplayName { get; set; }

    /// <summary>
    /// Optional sort order for display. 
    /// </summary>
    /// <remarks>The smaller the number, the closer to the left it will be displayed.</remarks>
    public required int SortOrder { get; set; }

    /// <summary>
    /// Path to an icon file.
    /// </summary>
    public required string IconPath { get; set; }

    public ICollection<AssetCategory> Categories { get; set; } = [];
}
