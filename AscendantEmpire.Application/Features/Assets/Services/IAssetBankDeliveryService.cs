using AscendantEmpire.Application.Common;
using AscendantEmpire.Application.Data;
using AscendantEmpire.Application.Features.Assets.Classes;

using static AscendantEmpire.Application.Common.ServiceResultBase;

namespace AscendantEmpire.Application.Features.Assets.Services;

/// <summary>
/// Empties each of a Player's camps into the Settlement it feeds, once per Turn.
/// </summary>
/// <remarks>
/// <para>
/// <b>The one sanctioned exception to balances never moving between banks.</b> Everywhere else, connecting, disconnecting, merging and conquering are re-keys or
/// ownership changes; this is an explicit per-Turn transfer. It exists because a camp can already <i>spend</i> from its parent's stock by sharing its network, but
/// cannot <i>store</i>: its own capacity is tiny, so its production would hit the cap and be wasted.
/// </para>
/// <para>
/// <b>Three conditions, all needed.</b> The deliverer must be of camp tier, it must carry a <c>Settlement.ParentSettlementId</c> naming a Settlement that is not a camp,
/// and the two banks must share an <c>AssetBank.NetworkKey</c>.
/// Sever the link and delivery simply stops: the camp keeps collecting to its own cap and loses nothing it had, and the designation does not change. Reconnect and it
/// resumes. A camp with no parent at all is its own network of one and banks normally - the first Settlement of a game is a camp, and the Meeting Place is built in one.
/// </para>
/// <para>
/// <b>Deliver everything, keep nothing.</b> The camp is left holding no balance row at all, which costs it nothing because it still draws on the network it shares. The
/// accepted consequence is the other way round: a severed camp fills its small store and then wastes production, and that is the intended pain.
/// </para>
/// </remarks>
internal interface IAssetBankDeliveryService
{
    /// <summary>
    /// Moves every balance out of each eligible camp's bank and into its parent settlement's.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Works on the caller's context and does not save</b>, so the transfers commit with whatever else the end-of-Turn process is doing and a failure later in that
    /// unit of work takes them back.
    /// </para>
    /// <para>
    /// <b>Not guarded against a second run in one Turn.</b> It does not need to be: a camp emptied by the first run has nothing left for the second to move, so running
    /// it twice moves the same stock once. That is the one thing that makes it safe inside a pipeline with no retry story - see <c>Gathering</c>, which is not.
    /// </para>
    /// <para>
    /// <b>Nothing is clamped.</b> Per-Settlement capacity is RS-29 and does not exist yet, so a delivery cannot overfill a parent today. When it does, the cap belongs on
    /// the credit path with the rest of the spreading rules (RS-38), not here.
    /// </para>
    /// </remarks>
    /// <param name="gameId">The Game to deliver within, since a bank is located by its Settlement and a Player holds Settlements in one Game.</param>
    /// <param name="playerId">
    /// The Player whose camps to empty. A camp and a parent that have come to belong to different Players are never joined, because they cannot share a network.
    /// </param>
    Task<ServiceResult<AssetDeliveryOutcome>> DeliverCampProductionAsync(AscendantEmpireContext context, int gameId, int playerId, CancellationToken cancelToken = default);
}
