namespace AscendantEmpire.BackendContracts.MapGallery.SearchCampaignMap;

public record CampaignMapIndex
{
    public required int CampaignMapGalleryIndexId { get; init; }
    public required string MapName { get; init; }
    public required string Description { get; init; }
    public required int Width { get; init; }
    public required int Height { get; init; }

    /// <summary>Whether the map's East-West axis wraps, so the last column is adjacent to column 0.</summary>
    public required bool HorizontalWrap { get; init; }

    /// <summary>Whether the map's North-South axis wraps, so the last row is adjacent to row 0.</summary>
    public required bool VerticalWrap { get; init; }
}
