namespace AscendantEmpire.BackendContracts.MapGallery.SearchCampaignMap;

public record CampaignMapIndex
{
    public required int CampaignMapGalleryIndexId { get; init; }
    public required string MapName { get; init; }
    public required string Description { get; init; }
    public required int Width { get; init; }
    public required int Height { get; init; }
}
