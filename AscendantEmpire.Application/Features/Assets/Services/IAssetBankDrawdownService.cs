using AscendantEmpire.Application.Common;
using AscendantEmpire.Application.Data;
using AscendantEmpire.Application.Features.Assets.Classes;

namespace AscendantEmpire.Application.Features.Assets.Services;

/// <summary>
/// Spends an <see cref="AssetExpense"/> out of whatever holds the Asset, and refuses it whole when the Player cannot afford it.
/// </summary>
/// <remarks>
/// <para>
/// The single place anything is debited, so a build order, a technology purchase and an upkeep charge cannot disagree about what "the Player can afford
/// this" means. Where the amount comes <i>from</i> is production's business and not modelled here; this only spends.
/// </para>
/// <para>
/// <b>It routes on <c>Asset.StorageScope</c>.</b> A <c>SettlementBank</c> Asset is debited from the bank the expense's Settlement draws on, a <c>Player</c>
/// one from the player-level store that already holds its figure, and a <c>Derived</c> one cannot be spent at all - see
/// <see cref="Domain.Enums.Assets.AssetStorageScope"/>. That column is why this has no list of Assets it knows to be unspendable.
/// </para>
/// <para>
/// <b>Works on the caller's context and does not save.</b> A technology purchase debits the cost and writes the unlock in one <c>SaveChangesAsync</c>, so
/// neither can land without the other; the same holds for a build order and its construction row. See the remarks on <see cref="DebitAsync"/> for how a
/// failure leaves the context.
/// </para>
/// <para>See <c>docs/design/asset-banks.md</c> for the drawdown rules this implements.</para>
/// </remarks>
internal interface IAssetBankDrawdownService
{
    /// <summary>
    /// Stages the expense against the Player's holdings, or refuses it and stages nothing.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Every expense succeeds whole or changes nothing.</b> Sufficiency is settled across the entire expense before a single balance is touched, so a
    /// group expense that zeroes Oak and then finds Cedar short never happens - on refusal the context is exactly as it was handed over and the caller is
    /// free to carry on with it.
    /// </para>
    /// <para>The failure statuses are distinct on purpose, because they mean different things to a caller:</para>
    /// <list type="bullet">
    /// <item>
    /// <description>
    /// <b>Conflict</b> - the Player cannot afford it. The message says what was short and by how much, and <c>ErrorDetail</c> carries the same as an
    /// <see cref="BackendContracts.Assets.AssetShortfall"/> for a caller that has to do more than print it. This is the ordinary refusal, and the one a
    /// build order shows the Player.
    /// </description>
    /// </item>
    /// <item>
    /// <description><b>NotFound</b> - the Asset, group, Settlement bank or player-level row the expense needs is not there.</description>
    /// </item>
    /// <item>
    /// <description>
    /// <b>InternalServerError</b> - the expense should never have been made: a non-positive amount, or an Asset that is not spendable from where the expense
    /// points. Debiting a <c>Derived</c> Asset is this rather than a shortfall, because population is not something a Player ran out of.
    /// </description>
    /// </item>
    /// </list>
    /// </remarks>
    Task<ServiceResultBase> DebitAsync(AscendantEmpireContext context, AssetExpense expense, CancellationToken cancelToken = default);
}
