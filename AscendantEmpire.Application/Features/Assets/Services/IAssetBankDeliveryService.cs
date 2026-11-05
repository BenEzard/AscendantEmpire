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
/// <b>Deliver everything the parent's network can hold.</b> A camp is normally left holding no balance row at all, which costs it nothing because it still draws on the
/// network it shares. Where there is no room, the delivery is <b>clamped</b>: it moves what fits and the camp keeps the remainder to offer again next Turn, because
/// destroying stock for being undeliverable is worse than leaving it in a small store. The accepted consequence is the other way round: a severed camp fills its own store
/// and then wastes production, and that is the intended pain.
/// </para>
/// </remarks>
internal interface IAssetBankDeliveryService
{
    /// <summary>
    /// Moves every balance out of each eligible camp's bank and into the network its parent settlement sits in.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Works on the caller's context and does not save</b>, so the transfers commit with whatever else the end-of-Turn process is doing and a failure later in that
    /// unit of work takes them back.
    /// </para>
    /// <para>
    /// <b>One credit per camp balance row, placed through <see cref="IAssetBankCreditService"/></b>, which is what decides the bank each row lands in and clamps it
    /// against that Settlement's capacity. The credits are raised in ascending Asset id within each camp and placed in that order, so the store runs out partway down
    /// the list and the ordering decides what survives. Every credit goes in one call, so a second camp feeding the same network sees what the first delivered.
    /// </para>
    /// <para>
    /// <b>No camp is ever a destination</b>, the delivering one included: the spread is told to leave every camp bank the Player holds alone, because moving stock from
    /// one small store into another would gain nothing.
    /// </para>
    /// </remarks>
    /// <param name="gameId">The Game to deliver within, since a bank is located by its Settlement and a Player holds Settlements in one Game.</param>
    /// <param name="playerId">
    /// The Player whose camps to empty. A camp and a parent that have come to belong to different Players are never joined, because they cannot share a network.
    /// </param>
    Task<ServiceResult<AssetDeliveryOutcome>> DeliverCampProductionAsync(AscendantEmpireContext context, int gameId, int playerId, CancellationToken cancelToken = default);
}
