namespace AscendantEmpire.FrontendContracts.MapGallery.GetCampaignMapStartPositions;

public interface ICampaignMapStartPositionCollection
{
    int CampaignMapGalleryIndexId { get; }
    IReadOnlyCollection<ICampaignMapStartPosition> StartPositions { get; }
}
