using AscendantEmpire.Domain.Enums.Maps;

namespace AscendantEmpire.BackendContracts.CampaignMaps.GetPlayerCampaignMap;

public record class ObservedCampaignTile
{
    public int XCoord { get; init; }
    public int YCoord { get; init; }
    public BiomeType BiomeTypeId { get; init; }
    public CampaignMapGeographyType CampaignMapGeographyTypeId { get; init; }

    /// <summary>
    /// Whether the Player has ever seen this Tile, and so knows the lie of the land on it.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The permanent half of the fog of war, and a different question from whether anything is watching the Tile right now. Exploring a Tile reveals
    /// its Biome and Geography for good; the Structures and Forces standing on it are reported only while an Observer can actually see it, so an
    /// explored Tile that has fallen out of sight comes back as terrain with nothing on it.
    /// </para>
    /// <para>
    /// Backed by <c>PlayerCampaignMap.FirstSeenOnTurnId</c>, which this read sets as it goes. See <c>docs/fog-of-war.md</c>.
    /// </para>
    /// </remarks>
    public bool IsExplored { get; init; }

    /// <summary>
    /// The river on each of the Tile's four edges, or <see langword="null"/> when the Player has not explored it.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Derived server-side from the Tile's own <c>SouthRiver</c> and <c>EastRiver</c> and those of the Tiles above and to the left of it, with the map's
    /// wrapping honoured, so the client never has to know which side of an edge owns it.
    /// </para>
    /// <para>
    /// Rivers are terrain, so like the Biome and Geography they are reported for any explored Tile whether or not an Observer can see it now. An edge is
    /// therefore visible as soon as <b>either</b> of the Tiles it separates has been explored, since each of them describes it.
    /// </para>
    /// </remarks>
    public TileRivers? Rivers { get; init; }

    public IReadOnlyCollection<ObservedStructure> Structures { get; init; } = null!;
    public IReadOnlyCollection<ObservedForce> Forces { get; init; } = null!;
}
