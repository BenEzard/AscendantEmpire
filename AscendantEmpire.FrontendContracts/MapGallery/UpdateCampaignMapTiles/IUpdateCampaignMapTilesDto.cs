using AscendantEmpire.Domain.Enums.Maps;
using AscendantEmpire.FrontendContracts.Maps;

namespace AscendantEmpire.FrontendContracts.MapGallery.UpdateCampaignMapTiles;

public interface IUpdateCampaignMapTilesDto
{
    public IReadOnlyCollection<ICoord> Coordinates { get; }
    public BiomeType? BiomeType { get; }
    public CampaignMapGeographyType? GeographyType { get; }
}
