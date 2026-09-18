using AscendantEmpire.Domain.Enums.Maps;

namespace AscendantEmpire.Application.Features.Maps.MapGallery.Classes.Export;

/// <summary>
/// Snapshot of a CampaignBiome as it existed at export time.
/// Only biomes actually used by tiles in the map are included.
/// </summary>
public class MapExportBiome
{
    public BiomeType BiomeTypeId { get; init; }
    public required string Name { get; init; }
    public decimal MovementCost { get; init; }
    public bool DoesBlockSight { get; init; }
    public bool IsWater { get; init; }
    public bool IsTraversable { get; init; }
    public decimal? RegenerationPercent { get; init; }
    public bool IsPaintableOnCampaignMap { get; init; }
    public required string MapEditorHtmlColor { get; init; }
    public bool CanBeElevated { get; init; }
    public int? BaseForageValue { get; init; }
    public int? BaseHuntValue { get; init; }
    public int? BaseFishValue { get; init; }
    public int? BaseFodderValue { get; init; }
    public int? BaseBoneValue { get; init; }
    public int? BaseWoodValue { get; init; }
    public int? BaseStoneValue { get; init; }
}
