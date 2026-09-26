using AscendantEmpire.Domain.Enums.Maps;

namespace AscendantEmpire.FrontendContracts.CampaignMaps;

public interface IObservedCampaignTile
{
    int XCoord { get; }
    int YCoord { get; }
    BiomeType BiomeTypeId { get; }
    CampaignMapGeographyType CampaignMapGeographyTypeId { get; }

    /// <summary>
    /// Whether the Player has ever seen this Tile, and so knows the lie of the land on it.
    /// </summary>
    /// <remarks>
    /// The permanent half of the fog of war. It is a different question from whether the Tile is being watched right now: a Tile arrives here
    /// because some Observer can see it, or because the Player explored it earlier and the whole-map read is handing back what they remember.
    /// The client draws solid fog over what is not explored, and a lighter haze over what is explored but unwatched. See <c>docs/fog-of-war.md</c>.
    /// </remarks>
    bool IsExplored { get; }

    /// <summary>
    /// The river on each of the Tile's four edges, or <see langword="null"/> when the Player has not explored it.
    /// </summary>
    /// <remarks>
    /// Terrain, like the Biome and Geography: reported for any explored Tile whether or not something is watching it now. Every edge is reported by both
    /// of the Tiles it separates, so the client draws each one once — see <c>TileGrid</c>.
    /// </remarks>
    ITileRivers? Rivers { get; }

    IReadOnlyCollection<IStructureStub>? Structures { get; }
    IReadOnlyCollection<IForceStub>? Forces { get; }
}
