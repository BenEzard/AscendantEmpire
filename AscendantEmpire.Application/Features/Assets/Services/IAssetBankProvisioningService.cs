using AscendantEmpire.Application.Common;
using AscendantEmpire.Application.Data;
using AscendantEmpire.Domain.Entities.Assets;
using AscendantEmpire.Domain.Entities.Settlements;
using AscendantEmpire.Domain.Entities.Units;

using static AscendantEmpire.Application.Common.ServiceResultBase;

namespace AscendantEmpire.Application.Features.Assets.Services;

/// <summary>
/// Gives a newly founded Settlement the <see cref="AssetBank"/> it is born with, and a Unit the cargo bank it earns by carrying something.
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
/// <b>A Settlement is born with its bank; a Unit is not.</b> A Settlement cannot do anything at all without one - nothing it builds could have a cost - while
/// most Units never carry anything, so a bank per Unit fielded would be a row per Unit standing empty for the whole game. Cargo is therefore provisioned on
/// first use, which is what makes <see cref="ProvisionForUnitAsync"/> a get-or-create rather than a founding.
/// </para>
/// <para>
/// Only the bank a Settlement is <i>born</i> with. A Settlement's bank is its bank for the Settlement's whole life, so there is no later founding to do:
/// what changes afterwards is the network it sits in, which <see cref="IAssetNetworkConnectivityService"/> owns, and who owns it, which a conquest moves
/// through <c>AssetBank.PlayerId</c> - see <see cref="IAssetBankConquestService"/>. A camp coming to share a village's stock is a shared <c>NetworkKey</c>
/// plus a parent designation (RS-35), never a merge of two banks.
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

    /// <summary>
    /// Finds the Unit's cargo bank, staging one keyed into a network of its own if it has never carried anything.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Get-or-create, and that is deliberate.</b> The caller is a capture or a transfer, which has no way to know whether this Unit has carried anything
    /// before; making it find out first would put the same query in both places and leave two answers to disagree.
    /// </para>
    /// <para>
    /// <b>Works on the caller's context and does not save</b>, so a capture provisions and credits inside one unit of work. A bank already staged in the
    /// change tracker is handed back as readily as a saved one, since a query cannot see it and a second insert would fail the unique index.
    /// </para>
    /// <para>
    /// The bank's owner is the Unit's Player. A capture that changes hands does so by moving <c>AssetBank.PlayerId</c>, exactly as a conquered Settlement's
    /// stock does.
    /// </para>
    /// </remarks>
    /// <param name="unit">The Unit whose cargo this is. It must already be saved: nothing can be carried by a Unit that is not fielded yet.</param>
    /// <returns>The Unit's bank, which may be newly staged and so may still carry an <c>Id</c> of 0 until the caller saves.</returns>
    Task<ServiceResult<AssetBank>> ProvisionForUnitAsync(AscendantEmpireContext context, UnitInstance unit, CancellationToken cancelToken = default);

    /// <summary>
    /// Stages the removal of a Unit's cargo bank and everything in it, for a Unit that is about to stop existing.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>The cargo goes with its carriers.</b> There is nowhere on a campaign tile to leave it - no tile store exists - so a destroyed Unit's load is
    /// destroyed with it rather than dropped. That is a rule the game may want to revisit; what it cannot do is nothing, because the foreign key from the bank
    /// to the Unit would refuse the delete.
    /// </para>
    /// <para>
    /// <b>Works on the caller's context and does not save</b>, so the Unit and its cargo go in one unit of work. A Unit with no bank is the ordinary case and
    /// succeeds having staged nothing.
    /// </para>
    /// </remarks>
    Task<ServiceResultBase> RemoveForUnitAsync(AscendantEmpireContext context, int unitInstanceId, CancellationToken cancelToken = default);
}
