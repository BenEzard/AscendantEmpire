namespace AscendantEmpire.FrontendContracts.MapGallery.GetCampaignMapStartPositions;

public interface ICampaignMapStartPosition
{
    int Id { get; }
    int CampaignMapGalleryIndexId { get; }
    int XCoord { get; }
    int YCoord { get; }
    string? DisplayName { get; }
    int? LocationScore { get; }
}
