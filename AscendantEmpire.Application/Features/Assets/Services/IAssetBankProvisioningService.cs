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
/// <b>It owns the tier rule, not the caller.</b> Banks apply from <c>SettlementTier.Village</c> up and never to a <c>Camp</c>, and that is an asset-bank rule
/// rather than a fact about the tier, so it lives here instead of in <c>SettlementTierRules</c>. A caller may hand over any Settlement and get the right
/// answer; the settle path does not need a <c>switch</c> over tiers to stay correct.
/// </para>
/// <para>
/// Only the bank a Settlement is <i>born</i> with. Repointing a membership when settlements connect, folding one bank's balances into another when they merge,
/// and attaching a camp to the village it draws on are each their own ticket, and each will land as a method beside this one.
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
    /// <b>A tier below Village succeeds having staged nothing.</b> A camp draws on the village it attaches to rather than banking for itself, so a settle path
    /// that founds one has nothing to do here and is not made to know that.
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
