using AscendantEmpire.Domain.Enums.Maps;

namespace AscendantEmpire.FrontendContracts.MapGallery.GetCampaignMap;

public interface ICampaignMapTile
{
    public int CampaignMapGalleryIndexId { get; }
    public int XCoord { get; }
    public int YCoord { get; }
    public BiomeType BiomeTypeId { get; }
    public CampaignMapGeographyType CampaignMapGeographyTypeId { get; }
}
