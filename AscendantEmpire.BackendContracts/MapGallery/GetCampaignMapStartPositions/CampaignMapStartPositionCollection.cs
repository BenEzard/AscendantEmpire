using AscendantEmpire.FrontendContracts.MapGallery.GetCampaignMapStartPositions;

namespace AscendantEmpire.BackendContracts.MapGallery.GetCampaignMapStartPositions;

public record CampaignMapStartPositionCollection : ICampaignMapStartPositionCollection
{
    public int CampaignMapGalleryIndexId { get; init; }
    public IReadOnlyCollection<ICampaignMapStartPosition> StartPositions { get; init; } = [];
}
