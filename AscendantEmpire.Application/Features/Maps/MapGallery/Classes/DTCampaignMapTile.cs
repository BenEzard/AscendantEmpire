using AscendantEmpire.Domain.Enums.Maps;
using AscendantEmpire.FrontendContracts.MapGallery.GetCampaignMap;

namespace AscendantEmpire.Application.Features.Maps.MapGallery.Classes;

public class DTCampaignMapTile : ICampaignMapTile
{
    public int CampaignMapGalleryIndexId { get; set; }
    public int XCoord { get; set; }
    public int YCoord { get; set; }
    public BiomeType BiomeTypeId { get; set; }
    public CampaignMapGeographyType CampaignMapGeographyTypeId { get; set; }
}
