using AscendantEmpire.Domain.Enums;
using AscendantEmpire.Domain.Enums.Maps;

namespace AscendantEmpire.BackendContracts.CampaignMaps.GetPlayerCampaignMap;

public record class MapObserver
{
    /// <summary>
    /// Id of the observer.
    /// </summary>
    public required int Id { get; init; }
    public required RecordType RecordType { get; init; }
    public required int PlayerId { get; init; }

    /// <summary>
    /// The location of the observer on the campaign map.
    /// </summary>
    public required Coord Location { get; init; }

    /// <summary>
    /// The sight range of the observer, in tiles.
    /// </summary>
    /// <remarks>Sight Range is used to see Tiles and Structures</remarks>
    public required int SightRange { get; init; }

    /// <summary>
    /// Optional directional mode. Defaults to FullRadius (existing behavior).
    /// </summary>
    public DetectionDirection SightDirection { get; init; } = DetectionDirection.FullRadius;
}