using AscendantEmpire.FrontendContracts.CampaignMaps;

namespace AscendantEmpire.Application.Handlers.Maps.CampaignMaps.Classes;

public class DTObservedCampaignTileCollection : IObservedCampaignTileCollection
{
    public IReadOnlyCollection<IObservedCampaignTile> Tiles { get; set; }
}
