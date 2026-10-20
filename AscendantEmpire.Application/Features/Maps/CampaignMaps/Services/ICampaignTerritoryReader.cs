using AscendantEmpire.Application.Data;
using AscendantEmpire.Application.Features.Maps.CampaignMaps.Classes;

namespace AscendantEmpire.Application.Features.Maps.CampaignMaps.Services;

/// <summary>
/// Loads the tiles the per-Round territory pass runs over, and the Players it runs for.
/// </summary>
/// <remarks>
/// <para>
/// The seam between the database and <see cref="TileTerritory"/>, in the same spirit as <see cref="ISightDataReader"/>: every query lives here so the rules stay pure and
/// testable on their own. Scalars only - the tiles are never materialised with their Biome and Geography navigations, because a whole Game's worth of those is the
/// difference between a few megabytes and far more.
/// </para>
/// <para>
/// Unlike <see cref="ICampaignOwnershipReader"/>, this reads the <i>whole</i> Game. The map read asks about a handful of tiles in sight; the pass is a graph problem over
/// everything, which is why <c>docs/design/campaign-map/tile-ownership.md</c> puts it in memory rather than in SQL.
/// </para>
/// </remarks>
internal interface ICampaignTerritoryReader
{
    /// <summary>
    /// Every tile of one Game, with its ownership columns, its claimants, the terrain the rules read and its river edges.
    /// </summary>
    /// <param name="context">The caller's context, so a read during a turn pipeline sees that pipeline's writes.</param>
    Task<IReadOnlyCollection<TerritoryTile>> GetTerritoryAsync(AscendantEmpireContext context, int gameId, CancellationToken cancelToken = default);

    /// <summary>
    /// Some of one Game's tiles, in the same shape <see cref="GetTerritoryAsync"/> returns.
    /// </summary>
    /// <remarks>
    /// For callers that already know the handful of tiles they are about to write - a Structure asserting over its own ground and a Settlement's radius - so the
    /// projection has one home rather than being spelt out again at each site.
    /// </remarks>
    /// <param name="campaignMapIds">The tiles to load. Duplicates are harmless; an empty set skips every query.</param>
    Task<IReadOnlyCollection<TerritoryTile>> GetTilesAsync(
        AscendantEmpireContext context,
        int gameId,
        IReadOnlyCollection<int> campaignMapIds,
        CancellationToken cancelToken = default);

    /// <summary>
    /// The Players active in one Game's campaign turn queue, earliest in the queue first.
    /// </summary>
    /// <remarks>
    /// Turn order rather than id order, because it is the tie-break: where two Players could want one tile, the one who takes their Turn first gets it. Reading it here
    /// keeps the pass itself free of any notion of a queue.
    /// </remarks>
    Task<IReadOnlyCollection<int>> GetPlayerIdsInTurnOrderAsync(AscendantEmpireContext context, int gameId, CancellationToken cancelToken = default);
}
