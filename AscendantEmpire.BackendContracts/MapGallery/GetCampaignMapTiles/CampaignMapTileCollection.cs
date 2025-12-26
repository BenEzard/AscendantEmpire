using AscendantEmpire.FrontendContracts.MapGallery.GetCampaignMap;
using AscendantEmpire.FrontendContracts.MapGallery.GetCampaignMapTiles;

namespace AscendantEmpire.BackendContracts.MapGallery.GetCampaignMapTiles;

public record CampaignMapTileCollection : ICampaignMapTileCollection
{
    public int CampaignMapGalleryIndexId { get; set; }

    public IReadOnlyCollection<ICampaignMapTile> Tiles { get; set; }
}
