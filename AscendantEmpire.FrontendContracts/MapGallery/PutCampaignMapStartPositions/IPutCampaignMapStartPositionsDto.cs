namespace AscendantEmpire.FrontendContracts.MapGallery.PutCampaignMapStartPositions;

public interface IPutCampaignMapStartPositionsDto
{
    IReadOnlyCollection<IPutCampaignMapStartPositionDto> StartPositions { get; }
}
