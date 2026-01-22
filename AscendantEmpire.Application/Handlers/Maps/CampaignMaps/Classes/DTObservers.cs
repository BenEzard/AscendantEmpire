using AscendantEmpire.FrontendContracts.CampaignMaps;

namespace AscendantEmpire.Application.Handlers.Maps.CampaignMaps.Classes;

public class DTObservers : IMapObservers
{
    public IReadOnlyCollection<IObserver> Observers { get; set; }
}
