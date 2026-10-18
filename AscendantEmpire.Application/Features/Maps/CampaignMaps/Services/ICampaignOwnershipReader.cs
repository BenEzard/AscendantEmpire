using AscendantEmpire.Application.Data;
using AscendantEmpire.Application.Features.Maps.CampaignMaps.Classes;
using AscendantEmpire.BackendContracts.CampaignMaps.GetPlayerCampaignMap;

namespace AscendantEmpire.Application.Features.Maps.CampaignMaps.Services;

/// <summary>
/// Reads the world map's ownership columns, its claims and the battles on it, and hands back each tile's state.
/// </summary>
/// <remarks>
/// <para>
/// The seam between the database and <see cref="TileOwnershipDerivation"/>, in the same spirit as <see cref="ICampaignRiverReader"/>: every query lives here and the
/// precedence between the states stays pure and testable on its own.
/// </para>
/// <para>
/// Ownership is read from <c>game.CampaignMap</c>, the world map, never from a Player's copy. A Player's map carries no ownership columns, and deliberately so: a border is
/// current information, so the map read reports it only where an Observer can see it rather than leaving the Player a remembered one to read - see
/// <see cref="ITileOwnership"/>.
/// </para>
/// </remarks>
internal interface ICampaignOwnershipReader
{
    /// <summary>
    /// The ownership state of each of <paramref name="campaignMapIds"/>, keyed by world-map tile id.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Only the tiles asked about come back, which is what keeps fog of war out of this class: the caller passes the ground an Observer can see, and nothing else is loaded
    /// or reported.
    /// </para>
    /// <para>
    /// A tile with no row behind it - which should not happen on a Game whose map is fully written - is absent from the result rather than reported as wilderness, so a
    /// caller never mistakes a missing tile for an unowned one.
    /// </para>
    /// </remarks>
    /// <param name="context">The caller's context, so a read during the end-of-turn pipeline sees that pipeline's writes.</param>
    /// <param name="gameId">The Game whose world map to read.</param>
    /// <param name="campaignMapIds">The tiles to describe. Duplicates are harmless; an empty set skips every query.</param>
    Task<IReadOnlyDictionary<int, TileOwnership>> GetTileOwnershipAsync(
        AscendantEmpireContext context,
        int gameId,
        IReadOnlyCollection<int> campaignMapIds,
        CancellationToken cancelToken = default);
}
