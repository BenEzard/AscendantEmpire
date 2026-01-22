using AscendantEmpire.FrontendContracts.CampaignMaps;

namespace AscendantEmpire.Application.Handlers.Maps.CampaignMaps.Classes;

public class DTObserver : IObserver
{
    public int TemporaryObserverId { get; set; }

    public int XCoord { get; set; }
    public int YCoord { get; set; }

    public IReadOnlyCollection<IObservedCampaignTile> SeenTiles { get; set; }
}
