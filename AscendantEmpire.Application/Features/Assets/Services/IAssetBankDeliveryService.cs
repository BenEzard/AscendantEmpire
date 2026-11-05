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
/// <b>Deliver everything the parent can hold.</b> A camp is normally left holding no balance row at all, which costs it nothing because it still draws on the network it
/// shares. Where the parent has no room, the delivery is <b>clamped</b>: it takes what fits and the camp keeps the remainder to offer again next Turn, because destroying
/// stock for being undeliverable is worse than leaving it in a small store. The accepted consequence is the other way round: a severed camp fills its own store and then
/// wastes production, and that is the intended pain.
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
    /// <b>Clamped against the parent's capacity</b>, per Asset in ascending Asset id, through <c>IAssetBankCapacityService</c>. One capacity per parent Settlement is
    /// taken from as each row lands, so several Assets of one storage class cannot each be told the whole store is free and a second camp feeding one parent sees what
    /// the first delivered. Spreading a credit across the rest of a network is RS-38 and is not done here.
    /// </para>
    /// </remarks>
    /// <param name="gameId">The Game to deliver within, since a bank is located by its Settlement and a Player holds Settlements in one Game.</param>
    /// <param name="playerId">
    /// The Player whose camps to empty. A camp and a parent that have come to belong to different Players are never joined, because they cannot share a network.
    /// </param>
    Task<ServiceResult<AssetDeliveryOutcome>> DeliverCampProductionAsync(AscendantEmpireContext context, int gameId, int playerId, CancellationToken cancelToken = default);
}
