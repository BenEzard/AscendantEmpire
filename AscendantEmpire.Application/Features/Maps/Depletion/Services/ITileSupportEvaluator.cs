using AscendantEmpire.Application.Data;
using AscendantEmpire.Application.Features.Maps.CampaignMaps.Classes;
using AscendantEmpire.Domain.Enums.Resources;

using static AscendantEmpire.Application.Common.ServiceResultBase;

namespace AscendantEmpire.Application.Features.Maps.Depletion.Services;

/// <summary>
/// Works out the support level of campaign map tiles: whether each can feed the Player's Forces standing on it, together with any Forces about to move onto it.
/// </summary>
/// <remarks>
/// <para>
/// The one place a support level is worked out, shared by the tile toolbar (one tile, no requested Forces) and the hover marker (every observed tile, the selected Forces
/// requested), so the two can never colour the same tile differently.
/// </para>
/// <para>
/// Sight is the caller's business: only tiles the Player can see right now should be passed in, since a level discloses what is left on a tile, and that changes as
/// anyone feeds on it.
/// </para>
/// </remarks>
internal interface ITileSupportEvaluator
{
    /// <summary>
    /// The support level of each of <paramref name="tiles"/>.
    /// </summary>
    /// <param name="context">The caller's context, read through and never written to.</param>
    /// <param name="gameId">The Game the tiles are in.</param>
    /// <param name="playerId">The Player whose food values and Forces are used.</param>
    /// <param name="tiles">The tiles to evaluate, as the Player sees them.</param>
    /// <param name="forceIds">
    /// Forces to count on every tile as well as those already standing on it - the Forces the Player has selected, for the hover marker. Each must be one of the Player's own
    /// Forces in the Game; any other id fails the evaluation. Empty for the tile toolbar.
    /// </param>
    /// <returns>Each tile's level keyed by its <c>CampaignMapId</c>, <see langword="null"/> where there is nothing to say (food, but nobody to feed).</returns>
    Task<ServiceResult<IReadOnlyDictionary<int, TileSupportLevel?>>> EvaluateAsync(
        AscendantEmpireContext context,
        int gameId,
        int playerId,
        IReadOnlyCollection<SightTerrain> tiles,
        IReadOnlyCollection<int> forceIds,
        CancellationToken cancelToken = default);
}
