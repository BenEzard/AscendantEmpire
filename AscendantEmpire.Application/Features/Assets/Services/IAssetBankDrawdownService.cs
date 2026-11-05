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
/// this" means. Where the amount comes <i>from</i> is production's business and <see cref="IAssetBankCreditService"/>'s; this only spends.
/// </para>
/// <para>
/// <b>It routes on <c>Asset.StorageScope</c>.</b> A <c>SettlementBank</c> Asset is debited from a bank, a <c>Player</c> one from the player-level store that
/// already holds its figure, and a <c>Derived</c> one cannot be spent at all - see <see cref="Domain.Enums.Assets.AssetStorageScope"/>. That column is why
/// this has no list of Assets it knows to be unspendable.
/// </para>
/// <para>
/// <b>And then on the expense's own arm.</b> A banked Asset is spent out of the network the expense's Settlement draws on, or, where the expense names no
/// Settlement, across every bank the Player owns - see <see cref="AssetExpense.IsEmpireWide"/>:
/// </para>
/// <list type="bullet">
/// <item>
/// <description>
/// <b>At a Settlement</b> - a build order or an upkeep charge, which happens somewhere. It drains that Settlement's own bank first and takes the remainder
/// from the rest of its network, refused only when the network between them is short. Nothing is moved and no bank is topped up, so the depleted site stays
/// depleted. RS-27 adds the transport turn cost the distance implies.
/// </description>
/// </item>
/// <item>
/// <description>
/// <b>Empire-wide</b> - a technology cost or an export, which belongs to no Settlement. Availability is the sum across every bank, and the debit is spread
/// by levelling so that the fullest pools pay and a small one keeps the stock its own construction will need. It carries no transport cost.
/// </description>
/// </item>
/// </list>
/// <para>
/// An Asset held per Player can only be spent Empire-wide. Naming a Settlement for one is a caller that has the two arms confused, and is refused as such
/// rather than quietly ignored.
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
    /// free to carry on with it. That settling is across every bank the expense can reach at once - the Settlement's network, or every bank the Player owns -
    /// rather than bank by bank.
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
    /// <b>InternalServerError</b> - the expense should never have been made: an Asset that is not spendable from where the expense points, or a Settlement
    /// named for an Asset that is held per Player. Debiting a <c>Derived</c> Asset is this rather than a shortfall, because population is not something a
    /// Player ran out of.
    /// </description>
    /// </item>
    /// </list>
    /// <para>
    /// <b>An expense is built from the game's own cost data, not from a request</b>, which is why those malformed cases are a server error rather than a bad
    /// request: a cost naming a <c>Derived</c> Asset, or a fractional number of whole research points, is this code getting it wrong rather than the Player
    /// asking for something silly. An endpoint that ever takes an Asset or group id from its caller has to validate it before building the expense, because
    /// only the endpoint knows the id came from outside.
    /// </para>
    /// <para>
    /// <b>Two mistakes are not statuses at all.</b> A non-positive amount cannot be built into an <see cref="AssetExpense"/> in the first place, and a
    /// storage scope this has no arm for throws, because each is a build-time error rather than anything a request could provoke.
    /// </para>
    /// </remarks>
    Task<ServiceResultBase> DebitAsync(AscendantEmpireContext context, AssetExpense expense, CancellationToken cancelToken = default);
}
