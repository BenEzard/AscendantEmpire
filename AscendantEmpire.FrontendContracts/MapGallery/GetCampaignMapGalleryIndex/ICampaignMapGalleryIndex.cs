using AscendantEmpire.Domain.Enums.Maps;

namespace AscendantEmpire.FrontendContracts.MapGallery.GetCampaignMapGalleryIndex;

public interface ICampaignMapGalleryIndex
{
    int CampaignMapGalleryIndexId { get; }
    string MapName { get; }
    string Description { get; }
    int Width { get; }
    int Height { get; }

    /// <summary>
    /// Whether the map's East-West axis wraps, so the last column is adjacent to column 0.
    /// </summary>
    /// <remarks>
    /// The map creator's recommendation, which the game creator may override. It decides whether the last column's East edge is a real edge shared with
    /// column 0 or one facing off the map, which is what the river authoring rules turn on.
    /// </remarks>
    bool HorizontalWrap { get; }

    /// <summary>
    /// Whether the map's North-South axis wraps, so the last row is adjacent to row 0.
    /// </summary>
    bool VerticalWrap { get; }

    int CampaignMapOffsetId { get; }
    MapCreationStatus CreationStatus { get; }
    MapCreationEngine CreationEngine { get; }
    string? PropertiesJson { get; }
    string Status { get; }
    string? ErrorMessage { get; }
}
