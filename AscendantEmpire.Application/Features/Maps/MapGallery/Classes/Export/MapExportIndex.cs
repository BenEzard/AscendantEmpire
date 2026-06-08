using AscendantEmpire.Domain.Enums.Maps;

namespace AscendantEmpire.Application.Features.Maps.MapGallery.Classes.Export;

/// <summary>
/// Serialised form of the CampaignMapGalleryIndex record within a map export.
/// DB-generated IDs are deliberately excluded; all data here is portable.
/// </summary>
public class MapExportIndex
{
    public required string MapName { get; init; }
    public string Description { get; init; } = string.Empty;
    public int Width { get; init; }
    public int Height { get; init; }
    public MapCreationEngine CreationEngine { get; init; }
    public bool HorizontalWrap { get; init; }
    public bool VerticalWrap { get; init; }

    /// <summary>JSON blob of map creation parameters.</summary>
    public required string PropertiesJson { get; init; }

    public int CampaignMapOffsetId { get; init; }
}
