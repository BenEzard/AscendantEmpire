using AscendantEmpire.Domain.Enums.Maps;

namespace AscendantEmpire.Application.Features.Maps.MapGallery.Classes.Export;

/// <summary>
/// Snapshot of a CampaignGeography as it existed at export time.
/// Only geographies actually used by tiles in the map are included.
/// </summary>
public class MapExportGeography
{
    public CampaignMapGeographyType GeographyTypeId { get; init; }
    public required string Name { get; init; }
    public int RelativeElevation { get; init; }
    public decimal MovementCost { get; init; }
    public bool IsWater { get; init; }
    public bool IsTraversable { get; init; }
    public string? MapEditorIcon { get; init; }
    public bool IsPaintableOnCampaignMap { get; init; }
}
