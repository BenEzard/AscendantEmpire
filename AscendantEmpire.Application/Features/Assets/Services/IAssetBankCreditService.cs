using AscendantEmpire.Application.Data;
using AscendantEmpire.Application.Features.Assets.Classes;

using static AscendantEmpire.Application.Common.ServiceResultBase;

namespace AscendantEmpire.Application.Features.Assets.Services;

/// <summary>
/// Places produced stock into a Player's banks, spread across the receiving Settlement's network and bounded by what each Settlement can hold.
/// </summary>
/// <remarks>
/// <para>
/// The single place anything is credited, so a camp delivery and a turn of production cannot disagree about where stock lands or how much of it fits. Where the
/// amount came <i>from</i> is production's business and not modelled here; this only places.
/// </para>
/// <para>
/// <b>The spread favours the emptiest banks</b>, filling the lowest balances of that Asset up to a common water-line and leaving the fullest alone - the mirror
/// of the debit's levelling, and the only place levelling now lives. It is what re-levels a network after a local-first expense: the Settlement that just paid
/// for a building holds least of what it spent, so it takes the largest share of the next credit.
/// </para>
/// <para>
/// <b>Each bank is bounded by its own Settlement's capacity</b>, for the storage class the Asset is kept under, through
/// <see cref="IAssetBankCapacityService"/>. Capacity binds per Settlement and not per network, so a bank with no room takes nothing and the credit goes to the
/// rest of the network instead.
/// </para>
/// <para>
/// <b>A credit places what fits and reports the rest.</b> Where every bank the spread reaches is full for that class the remainder is <i>unplaced</i>: nothing
/// is written for it and the figure comes back in <see cref="AssetCreditPlacement.RawUnplaced"/>. It is deliberately not a refusal - a one-unit overflow would
/// otherwise throw away a whole Turn's yield - so wasting the remainder, or refusing before the next extraction, is the caller's call. The pre-flight it would
/// refuse on is <see cref="IAssetBankCapacityService"/>, which this service asks the same question through.
/// </para>
/// <para>See "Crediting an asset" in <c>docs/design/asset-banks.md</c> for the rules this implements.</para>
/// </remarks>
internal interface IAssetBankCreditService
{
    /// <summary>
    /// Stages every credit against the Players' banks, in the order they are given.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Works on the caller's context and saves nothing</b>, like the rest of the asset bank services, so a Turn's production and whatever consumed it commit
    /// together.
    /// </para>
    /// <para>
    /// <b>One call per unit of work, with every credit in it.</b> The credits are applied in order against one capacity per Settlement and one set of balance
    /// rows, so a second credit sees what the first placed - which is what stops two Assets of one class each being told the whole store is free. Rows this call
    /// creates are tracked but unsaved, and a later call picks them up out of the change tracker rather than inserting a duplicate of a composite key.
    /// </para>
    /// <para>The failure statuses are distinct on purpose, because they mean different things to a caller:</para>
    /// <list type="bullet">
    /// <item><description><b>NotFound</b> - a credit names an Asset that is not in the catalogue, or a Settlement with no bank belonging to that Player.</description></item>
    /// <item>
    /// <description>
    /// <b>InternalServerError</b> - a credit names an Asset that is not kept in a settlement bank. A Player or Derived Asset has no balance row to add to, so
    /// crediting one is this code getting it wrong rather than anything the game could have produced.
    /// </description>
    /// </item>
    /// </list>
    /// <para>
    /// <b>There is no refusal for want of storage</b>, and a full network is a success carrying an unplaced figure. Running out of room is an ordinary
    /// consequence of a small store rather than a fault, and a Turn must end either way.
    /// </para>
    /// </remarks>
    /// <param name="credits">The credits to place, applied in order. An empty collection is a success that writes nothing.</param>
    Task<ServiceResult<IReadOnlyList<AssetCreditPlacement>>> CreditAsync(
        AscendantEmpireContext context,
        IReadOnlyList<AssetCredit> credits,
        CancellationToken cancelToken = default);
}
