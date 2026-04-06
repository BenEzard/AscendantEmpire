using AscendantEmpire.Domain.Enums.Maps;
using AscendantEmpire.FrontendContracts.MapGallery.UpdateCampaignMapTiles;

namespace AscendantEmpire.Application.Features.Maps.MapGallery.Classes;

public class DTTileUpdateFailTile : ITileUpdateFailTile
{
    public required DTCoord Coord { get; set; }

    public int DbId { get; set; }

    public BiomeType OriginalBiomeType { get; set; }

    public CampaignMapGeographyType OriginalGeographyType { get; set; }

    FrontendContracts.Maps.ICoord ITileUpdateFailTile.Coord => Coord;
}
