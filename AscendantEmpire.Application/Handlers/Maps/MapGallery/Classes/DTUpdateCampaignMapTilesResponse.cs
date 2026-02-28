using AscendantEmpire.FrontendContracts.MapGallery.UpdateCampaignMapTiles;

namespace AscendantEmpire.Application.Handlers.Maps.MapGallery.Classes;

public class DTUpdateCampaignMapTilesResponse : IUpdateCampaignMapTilesResponse
{
    public int TilesUpdated { get; set; }

    public IReadOnlyCollection<DTTileGroupUpdateFail>? Failures { get; set; }

    IReadOnlyCollection<ITileGroupUpdateFail>? IUpdateCampaignMapTilesResponse.Failures => Failures;
}
