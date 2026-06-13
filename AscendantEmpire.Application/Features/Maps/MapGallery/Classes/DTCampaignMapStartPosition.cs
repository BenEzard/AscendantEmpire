using AscendantEmpire.FrontendContracts.MapGallery.GetCampaignMapStartPositions;

namespace AscendantEmpire.Application.Features.Maps.MapGallery.Classes;

public class DTCampaignMapStartPosition : ICampaignMapStartPosition
{
    public int Id { get; set; }
    public int CampaignMapGalleryIndexId { get; set; }
    public int XCoord { get; set; }
    public int YCoord { get; set; }
    public string? DisplayName { get; set; }
    public int? LocationScore { get; set; }
}
