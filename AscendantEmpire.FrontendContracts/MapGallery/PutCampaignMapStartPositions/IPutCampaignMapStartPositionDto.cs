namespace AscendantEmpire.FrontendContracts.MapGallery.PutCampaignMapStartPositions;

public interface IPutCampaignMapStartPositionDto
{
    int XCoord { get; }
    int YCoord { get; }
    string? DisplayName { get; }
    int? LocationScore { get; }
}
