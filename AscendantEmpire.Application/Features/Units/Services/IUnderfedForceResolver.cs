using AscendantEmpire.Application.Data;
using AscendantEmpire.Application.Features.Units.Classes;

namespace AscendantEmpire.Application.Features.Units.Services;

/// <summary>
/// Decides what happens to Forces that did not get all the food they needed at the end of their owner's Turn.
/// </summary>
/// <remarks>
/// <para>
/// The seam between gathering, which works out each Force's shortfall, and the consequences of going hungry, which are not designed yet. Gathering hands over every Force it
/// fed - underfed or not - so a consequence that depends on being well fed (recovery, morale) can plug in here too.
/// </para>
/// <para>
/// Called through gathering's own context <b>before</b> it saves, so anything written here (damage, a status) is staged into the same save as the depletion itself.
/// Implementations must not call <c>SaveChangesAsync</c>.
/// </para>
/// </remarks>
internal interface IUnderfedForceResolver
{
    /// <summary>
    /// Applies the consequences of feeding to the given Forces.
    /// </summary>
    /// <param name="context">Gathering's context. Stage writes on it; do not save.</param>
    /// <param name="gameId">The Game the Forces are in.</param>
    /// <param name="playerId">The Player whose Turn is ending, and who owns every Force given.</param>
    /// <param name="results">How well each Force fed.</param>
    Task ResolveAsync(AscendantEmpireContext context, int gameId, int playerId, IReadOnlyCollection<ForceFeedingResult> results, CancellationToken cancelToken = default);
}
