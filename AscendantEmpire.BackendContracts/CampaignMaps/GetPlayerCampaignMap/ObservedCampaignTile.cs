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
    public IReadOnlyCollection<ObservedStructure> Structures { get; init; } = null!;
    public IReadOnlyCollection<ObservedForce> Forces { get; init; } = null!;
}
