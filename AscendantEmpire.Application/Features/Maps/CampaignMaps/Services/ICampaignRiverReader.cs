using AscendantEmpire.Application.Data;
using AscendantEmpire.Application.Features.Maps.CampaignMaps.Classes;
using AscendantEmpire.BackendContracts.CampaignMaps;
using AscendantEmpire.BackendContracts.CampaignMaps.GetPlayerCampaignMap;

namespace AscendantEmpire.Application.Features.Maps.CampaignMaps.Services;

/// <summary>
/// Reads the world map's river columns and hands back the four edges of each tile asked about.
/// </summary>
/// <remarks>
/// <para>
/// The seam between the database and <see cref="TileRiverDerivation"/>, in the same spirit as <see cref="ISightDataReader"/>: every query lives here
/// and the rule — which tile owns which edge — stays pure and testable on its own.
/// </para>
/// <para>
/// Rivers are read from <c>game.CampaignMap</c>, the world map, not from a Player's copy. A Player's map carries no river columns, and it does not need
/// to: a river never changes, so there is nothing about it for a Player to remember differently from the world.
/// </para>
/// </remarks>
internal interface ICampaignRiverReader
{
    /// <summary>
    /// The four river edges of each of <paramref name="coords"/>, keyed by coordinate.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Only the tiles asked about come back, which is what keeps fog of war out of this class: the caller asks about the ground the Player has explored,
    /// and the neighbours whose columns own its North and West edges are loaded but never reported. An edge is therefore visible as soon as either of the
    /// tiles it separates is explored, because each of them describes it.
    /// </para>
    /// <para>
    /// A coordinate with no row behind it — which should not happen on a Game whose map is fully written — comes back with no rivers rather than being
    /// absent, so the caller never has to tell "no river" from "no tile".
    /// </para>
    /// </remarks>
    /// <param name="context">The caller's context, so a read during the end-of-turn pipeline sees that pipeline's writes.</param>
    /// <param name="gameId">The Game whose world map to read.</param>
    /// <param name="coords">The tiles to describe. Duplicates are harmless; an empty set skips the query entirely.</param>
    /// <param name="geometry">The map's size, wrapping and world-map id offset.</param>
    Task<IReadOnlyDictionary<Coord, TileRivers>> GetTileRiversAsync(
        AscendantEmpireContext context,
        int gameId,
        IReadOnlyCollection<Coord> coords,
        CampaignMapGeometry geometry,
        CancellationToken cancelToken = default);
}
