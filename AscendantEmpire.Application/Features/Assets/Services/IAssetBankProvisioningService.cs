using AscendantEmpire.Application.Common;
using AscendantEmpire.Application.Data;
using AscendantEmpire.Domain.Entities.Assets;
using AscendantEmpire.Domain.Entities.Settlements;

namespace AscendantEmpire.Application.Features.Assets.Services;

/// <summary>
/// Gives a newly founded Settlement the <see cref="AssetBank"/> it is born with.
/// </summary>
/// <remarks>
/// <para>
/// A Settlement with no bank holds nothing, so nothing it builds can have an acquisition cost or an upkeep. Founding and provisioning therefore belong to one
/// unit of work, which is why this stages onto the caller's context rather than owning a save of its own - see <see cref="ProvisionForNewSettlementAsync"/>.
/// </para>
/// <para>
/// <b>It does not write <c>NetworkKey</c>.</b> The key comes from <see cref="IAssetNetworkConnectivityService.AssignKeyToNewBank"/>, which is its only
/// writer: a second one would let the wrong Settlements reach each other with nothing in the schema to catch it.
/// </para>
/// <para>
/// <b>Every tier banks, a camp included.</b> There is no tier rule here and none for a caller to apply either: the first Settlement of a game is a camp, the
/// Meeting Place is built in one, and a camp with no bank of its own would have nowhere to put what that building produces.
/// </para>
/// <para>
/// Only the bank a Settlement is <i>born</i> with. A Settlement's bank is its bank for the Settlement's whole life, so there is no later founding to do:
/// what changes afterwards is the network it sits in, which <see cref="IAssetNetworkConnectivityService"/> owns, and who owns it, which a conquest moves
/// through <c>AssetBank.PlayerId</c> - see <see cref="IAssetBankConquestService"/>. A camp coming to share a village's stock is a shared <c>NetworkKey</c>
/// plus a feeder designation (RS-35), never a merge of two banks.
/// </para>
/// <para>See <c>docs/design/asset-banks.md</c> for the rules this serves.</para>
/// </remarks>
internal interface IAssetBankProvisioningService
{
    /// <summary>
    /// Stages the empty bank a newly founded Settlement is born with, keyed into a network of its own.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Works on the caller's context and does not save.</b> The Settlement may still be unsaved: the bank is joined through its navigation, so EF picks up
    /// the generated key when both are written in the same <c>SaveChangesAsync</c>, and a founding that rolls back takes the bank with it.
    /// </para>
    /// <para>
    /// <b>The tier is not consulted.</b> <c>docs/design/asset-banks.md</c> gives every Settlement a bank of its own, so there is no shape of Settlement this
    /// stages nothing for.
    /// </para>
    /// <para>
    /// <b>The bank is created empty.</b> Nothing seeds a starting quantity - a Player's starter assets grant unlocks, not amounts.
    /// </para>
    /// </remarks>
    /// <param name="playerId">
    /// The Player who will own the bank, and the only one who may draw on it. Passed rather than read off <see cref="Settlement.FoundingPlayerId"/>, which
    /// records who founded the Settlement and keeps saying so after a conquest moves it.
    /// </param>
    /// <returns>
    /// <c>InternalServerError</c> when the Settlement already has a bank, which the unique index on <c>AssetBank.SettlementId</c> would refuse anyway: a
    /// caller provisioning twice has mistaken a re-key or a conquest for a founding.
    /// </returns>
    Task<ServiceResultBase> ProvisionForNewSettlementAsync(AscendantEmpireContext context, int playerId, Settlement settlement, CancellationToken cancelToken = default);
}
