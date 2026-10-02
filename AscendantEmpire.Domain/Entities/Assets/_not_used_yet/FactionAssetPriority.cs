using AscendantEmpire.Domain.Entities.Assets;

namespace AscendantEmpire.Domain.Entities.Assets._not_used_yet;

/// <summary>
/// Global per-faction ordering of assets within a category.
/// Lower number = consumed first.
/// </summary>
public sealed class FactionAssetPriority
{
    public int FactionId { get; set; }
    public int AssetCategoryId { get; set; }
    public int AssetId { get; set; }

    public int PriorityOrder { get; set; }

    public AssetCategory Category { get; set; } = null!;
    public Asset Asset { get; set; } = null!;
}
