using AscendantEmpire.FrontendContracts.MapGallery.PutCampaignMapStartPositions;

namespace AscendantEmpire.BackendContracts.MapGallery.PutCampaignMapStartPositions;

public class PutCampaignMapStartPositionDto : IPutCampaignMapStartPositionDto
{
    public int XCoord { get; set; }
    public int YCoord { get; set; }
    public string? DisplayName { get; set; }
    public int? LocationScore { get; set; }
}
