using AscendantEmpire.FrontendContracts.MapGallery.GetCampaignMapStartPositions;

namespace AscendantEmpire.BackendContracts.MapGallery.GetCampaignMapStartPositions;

public record CampaignMapStartPosition : ICampaignMapStartPosition
{
    public int Id { get; init; }
    public int CampaignMapGalleryIndexId { get; init; }
    public int XCoord { get; init; }
    public int YCoord { get; init; }
    public string? DisplayName { get; init; }
    public int? LocationScore { get; init; }
}
