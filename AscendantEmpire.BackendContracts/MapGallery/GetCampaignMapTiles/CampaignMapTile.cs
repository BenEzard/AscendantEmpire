using AscendantEmpire.Domain.Enums.Maps;
using AscendantEmpire.FrontendContracts.MapGallery.GetCampaignMap;

namespace AscendantEmpire.BackendContracts.MapGallery.GetCampaignMapTiles;

public record CampaignMapTile : ICampaignMapTile
{
    public int CampaignMapGalleryIndexId { get; init; }
    public int XCoord { get; init; }
    public int YCoord { get; init; }
    public BiomeType BiomeTypeId { get; init; }
    public CampaignMapGeographyType CampaignMapGeographyTypeId { get; init; }
}
