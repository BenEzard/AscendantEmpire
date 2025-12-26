using AscendantEmpire.FrontendContracts.MapGallery.GetCampaignMap;

namespace AscendantEmpire.FrontendContracts.MapGallery.GetCampaignMapTiles;

public interface ICampaignMapTileCollection
{
    int CampaignMapGalleryIndexId { get; }
    IReadOnlyCollection<ICampaignMapTile> Tiles { get; }
}
