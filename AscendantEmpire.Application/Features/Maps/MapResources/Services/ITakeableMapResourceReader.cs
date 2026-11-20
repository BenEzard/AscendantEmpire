using AscendantEmpire.Application.Data;
using AscendantEmpire.Application.Features.Maps.MapResources.Classes;

namespace AscendantEmpire.Application.Features.Maps.MapResources.Services;

/// <summary>
/// Reads the MapResources standing on campaign map tiles that a Player's Forces could take from.
/// </summary>
/// <remarks>
/// <para>
/// The one read behind the Take Resources action, shared by the context-action builder that renders the button, the options endpoint that fills the dialog
/// and the handler that writes - so a resource the button offered is the resource the handler takes, judged by one set of conditions.
/// </para>
/// <para>
/// <b>Discovery is a condition here, unlike <c>ITileFoodStockReader</c>.</b> Feeding ignores it because a patch is physical food and a Force camped
/// on it eats whatever anyone has surveyed. Taking is a deliberate order the Player gives, and offering the button would disclose a MapResource they have
/// not found - so an undiscovered instance is simply not takeable.
/// </para>
/// <para>
/// Reads through the caller's context.
/// </para>
/// </remarks>
internal interface ITakeableMapResourceReader
{
    /// <summary>
    /// What is takeable on each of <paramref name="tileIds"/>, keyed by tile. A tile with nothing takeable on it is absent.
    /// </summary>
    /// <param name="playerId">
    /// The Player whose discovery, research and yield switches decide what is takeable and what it gives. Another Player standing on the same tile can see
    /// a different answer.
    /// </param>
    Task<IReadOnlyDictionary<int, IReadOnlyCollection<TakeableMapResource>>> GetForTilesAsync(
        AscendantEmpireContext context,
        int gameId,
        int playerId,
        IReadOnlyCollection<int> tileIds,
        CancellationToken cancelToken = default);
}
