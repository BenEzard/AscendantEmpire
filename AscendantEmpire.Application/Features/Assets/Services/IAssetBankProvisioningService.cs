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
/// <b>Every tier banks, a camp included.</b> There is no tier rule here and none for a caller to apply either: the first Settlement of a game is a camp, the
/// Meeting Place is built in one, and a camp with no bank of its own would have nowhere to put what that building produces - with no village to draw on, since
/// it is the Player's first Settlement. A camp coming to share a village's pool is a <i>merge</i> rather than an absence of a bank here.
/// </para>
/// <para>
/// Only the bank a Settlement is <i>born</i> with. Repointing a membership when settlements connect, and folding one bank's balances into another when two
/// merge - which is what a camp attaching to its village is - are each their own ticket, and each will land as a method beside this one.
/// </para>
/// <para>See <c>docs/design/asset-banks.md</c> for the rules this serves.</para>
/// </remarks>
internal interface IAssetBankProvisioningService
{
    /// <summary>
    /// Stages the empty bank a newly founded Settlement draws on, and its membership of it.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Works on the caller's context and does not save.</b> The Settlement may still be unsaved: the membership is joined through its navigation, so EF
    /// picks up the generated key when both are written in the same <c>SaveChangesAsync</c>, and a founding that rolls back takes the bank with it.
    /// </para>
    /// <para>
    /// <b>The tier is not consulted.</b> <c>docs/design/asset-banks.md</c> gives every Settlement a bank of its own, so there is no shape of Settlement this
    /// stages nothing for. A camp that later comes to share its village's pool keeps the membership staged here until a merge repoints it.
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
    /// <c>InternalServerError</c> when the Settlement is already in a bank, which the membership's primary key would refuse anyway: a caller provisioning
    /// twice has mistaken a merge or a repointing for a founding.
    /// </returns>
    Task<ServiceResultBase> ProvisionForNewSettlementAsync(AscendantEmpireContext context, int playerId, Settlement settlement, CancellationToken cancelToken = default);
}
