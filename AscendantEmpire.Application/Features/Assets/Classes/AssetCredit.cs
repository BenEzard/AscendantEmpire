using AscendantEmpire.Application.Helpers.ScaledNumerics;

namespace AscendantEmpire.Application.Features.Assets.Classes;

/// <summary>
/// One quantity to place in a Player's banks: which Asset, how much of it, and the Settlement whose network receives it.
/// </summary>
/// <remarks>
/// <para>
/// <b>A credit always arrives somewhere</b>, so there is no Empire-wide arm mirroring <see cref="AssetExpense.EmpireWide"/>. A debit can be met out of
/// everything a Player holds because nothing is travelling to meet it; a credit is stock that was extracted at a Settlement and has to be put down in a bank
/// with room for it.
/// </para>
/// <para>
/// <b>One Asset, never a group.</b> A group on the debit path is the Player's ordering of what they are willing to lose first; a credit is a quantity of one
/// thing that was produced, and there is nothing to order.
/// </para>
/// <para>See "Crediting an asset" in <c>docs/design/asset-banks.md</c> for how the spread is worked out.</para>
/// </remarks>
internal sealed record AssetCredit
{
    private AssetCredit()
    {
    }

    /// <summary>
    /// The Player receiving it, and the only one whose banks may be filled.
    /// </summary>
    public int PlayerId { get; private init; }

    /// <summary>
    /// The Settlement the credit arrives at, whose network it spreads across.
    /// </summary>
    /// <remarks>
    /// The Settlement rather than the bank, because that is what a caller has: production happens somewhere. Which banks that Settlement reaches is the credit
    /// service's business, and it changes as roads are built and pillaged.
    /// </remarks>
    public int SettlementId { get; private init; }

    /// <summary>
    /// The Asset being placed.
    /// </summary>
    public int AssetId { get; private init; }

    /// <summary>
    /// How much to place. Always positive.
    /// </summary>
    /// <remarks>
    /// Checked by the factory, which throws rather than build one. A zero credit is a caller that did not mean to call and a negative one is an expense by
    /// another name, which the drawdown owns - neither is something the game could have produced, so neither is a refusal the credit needs a status for.
    /// </remarks>
    public ScaledAmount Amount { get; private init; }

    /// <summary>
    /// Banks in the network the spread must not fill.
    /// </summary>
    /// <remarks>
    /// A camp delivery is what needs it: a delivery exists to get stock out of a small store and into a large one, so filling a camp's store - the source's own
    /// included - would gain nothing. Empty for ordinary production, which has no bank it must avoid.
    /// </remarks>
    public IReadOnlySet<int> ExcludedAssetBankIds { get; private init; } = new HashSet<int>();

    /// <summary>
    /// A quantity arriving at one Settlement, placed across the network that Settlement sits in.
    /// </summary>
    /// <param name="excludedAssetBankIds">Banks the spread must not fill, or null to let it reach the whole network.</param>
    public static AssetCredit AtSettlement(
        int playerId,
        int settlementId,
        int assetId,
        ScaledAmount amount,
        IReadOnlyCollection<int>? excludedAssetBankIds = null)
    {
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(amount, ScaledAmount.Zero);

        return new AssetCredit
        {
            PlayerId = playerId,
            SettlementId = settlementId,
            AssetId = assetId,
            Amount = amount,
            ExcludedAssetBankIds = excludedAssetBankIds is null ? new HashSet<int>() : new HashSet<int>(excludedAssetBankIds)
        };
    }
}
