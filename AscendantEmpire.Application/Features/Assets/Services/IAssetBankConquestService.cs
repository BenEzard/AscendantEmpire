using AscendantEmpire.Application.Common;
using AscendantEmpire.Application.Data;
using AscendantEmpire.Domain.Entities.Assets;

namespace AscendantEmpire.Application.Features.Assets.Services;

/// <summary>
/// Hands a conquered Settlement's <see cref="AssetBank"/> to its new owner, and puts both empires' networks right behind it.
/// </summary>
/// <remarks>
/// <para>
/// <b>A conquest is an ownership change and a re-key, never a transfer.</b> <see cref="AssetBank.SettlementId"/> never moves, no bank is created or dissolved, and no balance row is read, split
/// or written: the conqueror receives the full stock because the stock never went anywhere. That is what leaves this service with so little to do - the shape <c>docs/design/asset-banks.md</c>
/// settled on is what removed the bookkeeping.
/// </para>
/// <para>
/// <b>Both sides are recomputed, not just the conqueror's.</b> The captured Settlement may have been the bridge between two halves of the loser's empire, and losing it partitions them. Neither
/// half is this service's to work out, so it hands both Players to <see cref="IAssetNetworkConnectivityService.RecomputeForPlayerAsync"/> and that one rule decides.
/// </para>
/// <para>
/// <b>It has no caller.</b> Nothing in play takes a Settlement from its owner; siege and capture must come through here when they are built, alongside
/// <c>IStructureLifecycleWriter.CaptureAsync</c>, which moves who holds the Structure. Wiring one without the other leaves a Settlement whose new owner cannot spend its stock, or stock a
/// defeated Player is still spending.
/// </para>
/// <para>See <c>docs/design/asset-banks.md</c> for the conquest rules this serves.</para>
/// </remarks>
internal interface IAssetBankConquestService
{
    /// <summary>
    /// Moves the captured Settlement's bank to its new owner, isolates it, and recomputes the networks of both the Player who lost it and the Player who took it.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Saves, unlike the other asset services,</b> and saves twice. <see cref="IAssetNetworkConnectivityService.RecomputeForPlayerAsync"/> reads which banks belong to a Player from the
    /// database rather than from the change tracker, so a hand-over that had only been staged would have both Players assessed against ownership neither of them holds any more. The ownership move
    /// is therefore committed first and the recomputes run against it. Anything else the caller has pending is flushed with that first save.
    /// </para>
    /// <para>
    /// <b>The two saves land together.</b> A caller already inside a transaction keeps ownership of it and should discard the context on a failure; otherwise this opens one, and disposing it
    /// uncommitted rolls both saves back.
    /// </para>
    /// <para>
    /// <b>The captured bank arrives as a network of one</b>, and the conqueror's recompute is what joins it to anything they can actually reach. That is isolation today, since nothing in the
    /// codebase connects two Settlements yet; once CM-64, RS-48 and RS-49 widen what connectivity means, the same call picks the bank up with no change here. Keeping the loser's key instead would
    /// leave the captured bank sharing a network across the ownership boundary, which a per-Player consistency check can never see.
    /// </para>
    /// </remarks>
    /// <param name="settlementId">
    /// The captured Settlement. Its bank is found by this rather than by bank id because <see cref="AssetBank.SettlementId"/> is unique and never changes, so a capture that knows which Settlement
    /// fell needs nothing else to find the right store.
    /// </param>
    /// <param name="newOwnerPlayerId">
    /// The conqueror. The Player losing the bank is read off the bank rather than passed in, so a caller cannot name one who never owned it.
    /// </param>
    /// <returns>
    /// <c>NotFound</c> for a Settlement with no bank, which is a caller naming the wrong Settlement rather than a Settlement that genuinely has none - every Settlement is born with one. A generic
    /// failure where the conqueror already owns the bank, mirroring the capture of a Structure the Player already holds: there is no conquest to make.
    /// </returns>
    Task<ServiceResultBase> TransferOnConquestAsync(AscendantEmpireContext context, int settlementId, int newOwnerPlayerId, CancellationToken cancelToken = default);
}
