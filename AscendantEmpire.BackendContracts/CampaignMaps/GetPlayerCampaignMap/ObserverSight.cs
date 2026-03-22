using AscendantEmpire.Domain.Enums;

namespace AscendantEmpire.BackendContracts.CampaignMaps.GetPlayerCampaignMap;

public class ObserverSight
{
    /// <summary>
    /// Id of the observer.
    /// </summary>
    public required int Id { get; set; }
    public required RecordType RecordType { get; set; }
    public required Coord Location { get; set; }
    public required IReadOnlyCollection<ObservedCampaignTile> SeenTiles { get; set; }
}
