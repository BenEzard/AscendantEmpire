using AscendantEmpire.FrontendContracts.MapGallery.PutCampaignMapStartPositions;

namespace AscendantEmpire.BackendContracts.MapGallery.PutCampaignMapStartPositions;

public class PutCampaignMapStartPositionsDto : IPutCampaignMapStartPositionsDto
{
    public required IReadOnlyCollection<PutCampaignMapStartPositionDto> StartPositions { get; set; }

    IReadOnlyCollection<IPutCampaignMapStartPositionDto> IPutCampaignMapStartPositionsDto.StartPositions => StartPositions;
}
