namespace AscendantEmpire.FrontendContracts.CampaignMaps;

public interface IObserver
{
    public int TemporaryObserverId { get; }
    public int XCoord { get; }
    public int YCoord { get; }
    public IReadOnlyCollection<IObservedCampaignTile> SeenTiles { get; }
}
