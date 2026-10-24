using AscendantEmpire.Application.Helpers.ScaledNumerics;

namespace AscendantEmpire.Application.Features.Assets.Classes;

/// <summary>
/// One expense to draw out of a Player's holdings: what is being spent, how much of it, and whether it is paid at one Settlement or across the Empire.
/// </summary>
/// <remarks>
/// <para>
/// The two arms spend out of different banks and refuse for different reasons, so each is built through its own factory - <see cref="AtSettlement"/> and
/// <see cref="EmpireWide"/> - rather than by passing a Settlement that may or may not be meant. What is being spent is a <see cref="SpendTarget"/>, so the
/// Asset-or-group question stays in the argument that answers it instead of being spelled out in the name of the factory.
/// </para>
/// <para>
/// A Settlement expense carries the Settlement rather than the bank, because that is what the caller has: a build order happens somewhere. Which bank that
/// Settlement draws on is the drawdown's business, and it changes as roads are built and pillaged.
/// </para>
/// <para>See <c>docs/design/asset-banks.md</c> for what each arm spends and in what order.</para>
/// </remarks>
public sealed record AssetExpense
{
    private AssetExpense()
    {
    }

    /// <summary>
    /// The Player paying, and the only one whose holdings may be touched.
    /// </summary>
    public int PlayerId { get; private init; }

    /// <summary>
    /// The Settlement the expense is incurred at, whose bank pays it, or <see langword="null"/> where the expense is Empire-wide.
    /// </summary>
    /// <remarks>
    /// Never ignored. An expense that names a Settlement is paid out of that Settlement's bank alone, and one that names none is paid across every bank -
    /// the two are different spends rather than the same spend with a field that may or may not matter.
    /// </remarks>
    public int? SettlementId { get; private init; }

    /// <summary>
    /// What is being spent: one named Asset, or any Asset out of one group in the Player's ordering.
    /// </summary>
    public SpendTarget Target { get; private init; } = null!;

    /// <summary>
    /// How much to spend. Always positive.
    /// </summary>
    /// <remarks>
    /// Checked by both factories, which throw rather than build one. A zero expense is a caller that did not mean to call and a negative one is a credit by
    /// another name, which production owns - neither is something a Player could have done, so neither is a refusal the drawdown needs a status for.
    /// </remarks>
    public ScaledAmount Amount { get; private init; }

    /// <summary>
    /// Whether the expense is paid across every bank the Player owns rather than out of one Settlement's.
    /// </summary>
    /// <remarks>
    /// The absence of a Settlement is what makes an expense Empire-wide, so the two can never disagree. An Asset held per Player for the whole game can only
    /// be spent this way - see <c>AssetStorageScope.Player</c>.
    /// </remarks>
    public bool IsEmpireWide => SettlementId is null;

    /// <summary>
    /// A cost incurred somewhere - a build order, an upkeep charge - met from the bank that Settlement draws on, and from that bank alone.
    /// </summary>
    /// <remarks>
    /// A bank that is short refuses the expense rather than reaching further. Sourcing the remainder from the nearest bank that does hold it, and the
    /// transport turn cost that comes with doing so, is RS-27.
    /// </remarks>
    public static AssetExpense AtSettlement(int playerId, int settlementId, SpendTarget target, ScaledAmount amount)
    {
        ArgumentNullException.ThrowIfNull(target);
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(amount, ScaledAmount.Zero);

        return new AssetExpense
        {
            PlayerId = playerId,
            SettlementId = settlementId,
            Target = target,
            Amount = amount
        };
    }

    /// <summary>
    /// A cost that belongs to no Settlement - a technology, an export - met from every bank the Player owns.
    /// </summary>
    /// <remarks>
    /// Spread across the banks by levelling, so the fullest pools pay and a small one keeps the stock its own construction will need. It carries no transport
    /// cost: an Empire-wide expense is not goods arriving anywhere.
    /// </remarks>
    public static AssetExpense EmpireWide(int playerId, SpendTarget target, ScaledAmount amount)
    {
        ArgumentNullException.ThrowIfNull(target);
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(amount, ScaledAmount.Zero);

        return new AssetExpense
        {
            PlayerId = playerId,
            Target = target,
            Amount = amount
        };
    }
}
