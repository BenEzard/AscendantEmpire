using AscendantEmpire.Domain.Enums.Maps;

namespace AscendantEmpire.FrontendContracts.CampaignMaps;

/// <summary>
/// The river on each of a tile's four edges, as the client is told about them.
/// </summary>
/// <remarks>
/// <para>
/// Rivers run <b>between</b> tiles, and each edge is stored exactly once — on the tile to its north or to its west, as that tile's
/// <c>SouthRiver</c> or <c>EastRiver</c>. The server resolves that ownership before sending anything, so a tile arrives describing all four of its
/// own edges and the client never has to look at a neighbour to find out what borders it.
/// </para>
/// <para>
/// Both banks of a river therefore report the same edge. Drawing each one only once is the client's business, not the server's — see
/// <c>TileGrid</c>.
/// </para>
/// </remarks>
public interface ITileRivers
{
    /// <summary>The river along the tile's north edge — the edge it shares with the tile above it.</summary>
    RiverWidth North { get; }

    /// <summary>The river along the tile's east edge — the edge it shares with the tile to its right.</summary>
    RiverWidth East { get; }

    /// <summary>The river along the tile's south edge — the edge it shares with the tile below it.</summary>
    RiverWidth South { get; }

    /// <summary>The river along the tile's west edge — the edge it shares with the tile to its left.</summary>
    RiverWidth West { get; }
}
