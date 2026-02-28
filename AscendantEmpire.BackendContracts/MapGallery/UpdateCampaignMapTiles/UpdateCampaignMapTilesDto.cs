using AscendantEmpire.BackendContracts.CampaignMaps;
using AscendantEmpire.Domain.Enums.Maps;
using AscendantEmpire.FrontendContracts.MapGallery.UpdateCampaignMapTiles;

namespace AscendantEmpire.BackendContracts.MapGallery.UpdateCampaignMapTiles;

public class UpdateCampaignMapTilesDto : IUpdateCampaignMapTilesDto
{
    public required IReadOnlyCollection<Coord> Coordinates { get; set; }

    IReadOnlyCollection<AscendantEmpire.FrontendContracts.Maps.ICoord>
    IUpdateCampaignMapTilesDto.Coordinates => Coordinates;

    public BiomeType? BiomeType { get; set; }

    public CampaignMapGeographyType? GeographyType { get; set; }
}
