using AscendantEmpire.BackendContracts.CampaignMaps;

namespace AscendantEmpire.BackendContracts.CampaignMap;

public record class MapObserver
{
    /// <summary>
    /// A temporary identifier for this observer instance. This ID is only valid for the duration of the request/response cycle.
    /// </summary>
    public required int TemporaryObserverId { get; init; }

    /// <summary>
    /// The location of the observer on the campaign map.
    /// </summary>
    public required Coord Location { get; init; }

    /// <summary>
    /// The sight range of the observer, in tiles.
    /// </summary>
    /// <remarks>Sight Range is used to see Tiles and Structures</remarks>
    public required int SightRange { get; init; }
}
