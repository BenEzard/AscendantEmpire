namespace AscendantEmpire.FrontendContracts.MapGallery.SearchCampaignMapIndex;

public interface ICampaignMapIndex
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
    /// Sent with the search results because the map editor needs it as soon as a map is opened: the last column's East edge is a real edge shared with
    /// column 0 on a wrapping map, and an edge facing off the map on one that does not wrap.
    /// </remarks>
    bool HorizontalWrap { get; }

    /// <summary>
    /// Whether the map's North-South axis wraps, so the last row is adjacent to row 0.
    /// </summary>
    bool VerticalWrap { get; }
}
