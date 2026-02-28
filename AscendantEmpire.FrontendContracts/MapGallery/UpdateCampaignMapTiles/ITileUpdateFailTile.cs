using AscendantEmpire.Domain.Enums.Maps;
using AscendantEmpire.FrontendContracts.Maps;

namespace AscendantEmpire.FrontendContracts.MapGallery.UpdateCampaignMapTiles;

public interface ITileUpdateFailTile
{
    ICoord Coord { get; }

    int DbId { get; }

    BiomeType OriginalBiomeType { get; }
    CampaignMapGeographyType OriginalGeographyType { get; }
}