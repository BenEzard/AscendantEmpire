namespace AscendantEmpire.BackendContracts.CampaignMaps.GetPlayerCampaignMap;

public class ObserverSight
{
    /// <summary>
    /// A temporary identifier for this observer instance. This ID is only valid for the duration of the request/response cycle.
    /// </summary>
    public required int TemporaryObserverId { get; set; }
    public required Coord Location { get; set; }
    public required IReadOnlyCollection<ObservedCampaignTile> SeenTiles { get; set; }
}
