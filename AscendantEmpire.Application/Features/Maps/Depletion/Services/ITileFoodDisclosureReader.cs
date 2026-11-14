using AscendantEmpire.Application.Data;
using AscendantEmpire.Application.Features.Maps.Biomes.Classes;
using AscendantEmpire.Application.Features.Maps.CampaignMaps.Classes;
using AscendantEmpire.Application.Features.Maps.Depletion.Classes;
using AscendantEmpire.BackendContracts.CampaignMaps.GetPlayerCampaignMap;
using AscendantEmpire.Domain.Entities.Games;

namespace AscendantEmpire.Application.Features.Maps.Depletion.Services;

/// <summary>
/// What a tile's food comes to for one Player, in the individuals it feeds for a Turn, for the tile toolbar to show them.
/// </summary>
/// <remarks>
/// <para>
/// The same sources the end-of-Turn feed draws on (<see cref="TileFoodSources"/>), priced in the same rations, with two differences that follow from this
/// being disclosure rather than mechanics:
/// <list type="bullet">
///   <item><description>A patch counts only where the Player has <b>discovered</b> it. The feed ignores discovery - a Force camped on berries eats them
///   whether or not anyone surveyed them - but a total that counted an undiscovered patch would tell the Player food is there before they find
///   it.</description></item>
///   <item><description>A patch counts at the Player's own <b>estimate</b>, never its true quantity, exactly as the MapResource markers beside it on the
///   toolbar do. The support colour is worked out from the truth, so a tile whose estimate flatters it can be coloured worse than its total reads. That is
///   the estimate showing, not a defect.</description></item>
/// </list>
/// </para>
/// <para>
/// Only called for a tile in the Player's live sight, which is also what entitles a patch on it to a quantity at all.
/// </para>
/// <para>
/// Reads through the caller's context.
/// </para>
/// </remarks>
internal interface ITileFoodDisclosureReader
{
    /// <summary>
    /// The tile's food as the Player may be told it.
    /// </summary>
    /// <param name="tile">The tile, as one of the Player's Observers sees it.</param>
    /// <param name="values">The Player's effective sustenance values, which the caller has already read.</param>
    /// <param name="depletion">The tile's depletion row, or <see langword="null"/> when it has never been fed on.</param>
    /// <param name="rivers">The tile's four river edges, or <see langword="null"/> when they are not known.</param>
    Task<TileFoodDisclosure> GetAsync(
        AscendantEmpireContext context,
        int gameId,
        int playerId,
        SightTerrain tile,
        PlayerSustenanceValues values,
        CampaignMapDepletion? depletion,
        TileRivers? rivers,
        CancellationToken cancelToken = default);
}
