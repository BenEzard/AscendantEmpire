namespace AscendantEmpire.FrontendContracts.CampaignMaps;

public interface IObservedCampaignTileCollection
{
    public IReadOnlyCollection<IObservedCampaignTile> Tiles { get; }
}
