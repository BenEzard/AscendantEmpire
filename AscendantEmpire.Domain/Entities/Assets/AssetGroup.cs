namespace AscendantEmpire.Domain.Entities.Assets;

public sealed class AssetGroup
{
    /// <summary>
    /// Codes are hierarchical, so the limit has to hold a full dotted path rather than one segment - "ConstructionMaterials.Stone.Bricks.SandstoneBlock" is already 49.
    /// </summary>
    public const int MAX_CODE_LENGTH = 100;
    public const int MAX_DISPLAY_NAME_LENGTH = 50;

    public int Id { get; set; }

    /// <summary>
    /// Unique code for the Asset Group, e.g. "Logs" (within the <c>AssetCategory</c>).
    /// </summary>
    public required string Code { get; set; }

    /// <summary>
    /// Display name, e.g. "Logs". (e.g. within "Construction Materials" (AssetMetaCategory) -> "Wood" (AssetCategory) -> "Logs")
    /// </summary>
    public required string DisplayName { get; set; }

    public int AssetCategoryId { get; set; }

    /// <summary>
    /// Display order of this group within its Category. If not specified, will be based on Name.
    /// </summary>
    public int? SortOrder { get; set; }

    public required AssetCategory Category { get; set; }

    public ICollection<Asset> Assets { get; set; } = [];
}
