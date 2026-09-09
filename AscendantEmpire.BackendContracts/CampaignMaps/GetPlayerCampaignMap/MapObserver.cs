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

    /// <summary>
    /// Which terrain this Observer could cross, used only when dense terrain has blinded it.
    /// </summary>
    /// <remarks>
    /// An Observer standing on a sight-blocking Biome reveals the adjacent Tiles its domain could never have entered, so the reveal needs to know
    /// which those are. A Structure cannot move at all and has no domain of its own; it is set to <see cref="DomainType.Land"/> where it is built,
    /// so that a coastal Jungle settlement sees the sea beside it rather than sitting next to ground nothing can ever uncover.
    /// </remarks>
    public DomainType MovementDomain { get; init; } = DomainType.Land;
}