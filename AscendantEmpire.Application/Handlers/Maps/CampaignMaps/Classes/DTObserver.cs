using AscendantEmpire.FrontendContracts.CampaignMaps;

namespace AscendantEmpire.Application.Handlers.Maps.CampaignMaps.Classes;

public class DTObserver : IObserver
{
    public int Id { get; set; }
    public int ObserverRecordType { get; }

    public int XCoord { get; set; }
    public int YCoord { get; set; }

    public required IReadOnlyCollection<IObservedCampaignTile> SeenTiles { get; set; }

}
