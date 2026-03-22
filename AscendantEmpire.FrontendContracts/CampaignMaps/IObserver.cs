namespace AscendantEmpire.FrontendContracts.CampaignMaps;

public interface IObserver
{
    public int Id { get; }
    public int ObserverRecordType { get; }
    public int XCoord { get; }
    public int YCoord { get; }
    public IReadOnlyCollection<IObservedCampaignTile> SeenTiles { get; }
}
