namespace AscendantEmpire.FrontendContracts.CampaignMaps;

public interface IMapObservers
{
    public IReadOnlyCollection<IObserver> Observers { get; }
}
