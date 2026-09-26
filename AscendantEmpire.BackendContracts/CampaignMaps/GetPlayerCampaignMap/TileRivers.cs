using AscendantEmpire.Domain.Enums.Maps;
using AscendantEmpire.FrontendContracts.CampaignMaps;

namespace AscendantEmpire.BackendContracts.CampaignMaps.GetPlayerCampaignMap;

/// <inheritdoc cref="ITileRivers"/>
/// <remarks>
/// One record rather than four loose properties on <see cref="ObservedCampaignTile"/>, so the four edges travel together and a tile with no rivers at
/// all can be described by a single <see langword="null"/>.
/// </remarks>
public record class TileRivers : ITileRivers
{
    /// <summary>A tile with no river on any of its edges.</summary>
    /// <remarks>
    /// Still worth sending: it says the player knows the ground here and there is nothing on it, which is a different answer from the
    /// <see langword="null"/> an unexplored tile carries.
    /// </remarks>
    public static TileRivers None { get; } = new();

    public RiverWidth North { get; init; }
    public RiverWidth East { get; init; }
    public RiverWidth South { get; init; }
    public RiverWidth West { get; init; }

    /// <summary>Whether any of the four edges carries a river.</summary>
    public bool HasAny => North != RiverWidth.None || East != RiverWidth.None || South != RiverWidth.None || West != RiverWidth.None;
}
