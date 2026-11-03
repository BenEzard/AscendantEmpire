using AscendantEmpire.Application.Data;
using AscendantEmpire.Domain.Entities.Settlements;

using static AscendantEmpire.Application.Common.ServiceResultBase;

namespace AscendantEmpire.Application.Features.Settlements.Services;

/// <summary>
/// Works out which Settlement a camp being founded will deliver its production to.
/// </summary>
/// <remarks>
/// <para>
/// <b>The rule is in <c>docs/design/asset-banks.md</c>:</b> the nearest settlement of a tier above camp that the Player still holds; where two are equidistant, the one
/// the settler was created from; otherwise the lower settlement id. Nothing records which settlement a Force was raised at, so the middle tiebreaker is not applied yet -
/// see <see cref="ResolveAsync"/>.
/// </para>
/// <para>
/// <b>It resolves and nothing else.</b> The answer is returned rather than written, so the caller assigns <see cref="Settlement.ParentSettlementId"/> alongside the rest
/// of the founding's writes and there is no question of what this saves. "Resolve" rather than "detect" is the point: the parent is decided once, here, and then stored -
/// it is not a fact sitting in the data waiting to be found again.
/// </para>
/// <para>
/// <b>Stored by the caller, and never recomputed.</b> That middle tiebreaker is history, so a recompute could not reproduce it and would quietly re-attach the camp to the
/// other candidate. Storing the answer is also what lets the designation outlive a severed network: delivery stops and resumes while the column never moves.
/// </para>
/// <para>
/// <b>The answer is not the connection.</b> Whether the two banks can actually reach each other is <c>IAssetNetworkConnectivityService</c>'s to decide, and a delivery
/// needs both. Nothing here writes a <c>NetworkKey</c>.
/// </para>
/// </remarks>
internal interface ICampParentResolver
{
    /// <summary>
    /// The Settlement a camp on <paramref name="campStructureInstanceId"/>'s tile should feed, or <see langword="null"/> where the Player holds nothing to attach it to.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Reads only.</b> Nothing is staged on the context and nothing is saved, so a caller may resolve before it has saved the camp - which is the normal case, since a
    /// founding decides this inside its own transaction.
    /// </para>
    /// <para>
    /// <b>No candidate is not a failure.</b> The first settlement of a game is a camp, so founding one with nothing above it to feed is the ordinary opening move; the
    /// result succeeds with a null value and the camp banks for itself as its own network of one.
    /// </para>
    /// <para>
    /// <b>Candidates are the Player's own standing settlements of a tier above camp</b>, judged on <c>StructureInstance.OwnedByPlayerId</c> rather than
    /// <see cref="Settlement.FoundingPlayerId"/>, so a village that has changed hands is not offered to its founder's camps. Another camp is never offered either, which is
    /// also why the camp being founded cannot resolve to itself. Distance is the wrap-aware map distance, so a candidate across a joined edge is as near as the
    /// coordinates say it is.
    /// </para>
    /// <para>
    /// <b>The settler-origin tiebreaker is not applied.</b> Nothing in the model records which settlement a Force was raised at, and nothing produces Forces at a
    /// settlement yet, so two equidistant candidates are separated by the lower settlement id alone. Adding the origin later changes only which id is chosen at founding,
    /// because the caller stores the answer.
    /// </para>
    /// <para>
    /// <b>The tier of the settlement being founded is not checked here.</b> Only a camp has a parent, and the caller is the one that knows it is founding one - the settle
    /// handler already branches on the tier to decide whether to ask at all.
    /// </para>
    /// </remarks>
    /// <param name="playerId">The Player founding the camp, and the only one whose settlements may be its parent.</param>
    /// <param name="campStructureInstanceId">The Structure the camp stands on, which is the tile distance is measured from. It must already be saved.</param>
    /// <returns><c>InternalServerError</c> when the Game or the camp's own Structure cannot be read, in which case no distance can be measured.</returns>
    Task<ServiceResult<int?>> ResolveAsync(
        AscendantEmpireContext context,
        int gameId,
        int playerId,
        int campStructureInstanceId,
        CancellationToken cancelToken = default);
}
