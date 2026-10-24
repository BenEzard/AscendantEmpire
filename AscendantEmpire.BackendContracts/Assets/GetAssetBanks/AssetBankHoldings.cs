namespace AscendantEmpire.BackendContracts.Assets.GetAssetBanks;

/// <summary>
/// One asset bank: what it holds, and which Settlements can reach it.
/// </summary>
/// <remarks>
/// <para>
/// <b>The memberships travel with the balances deliberately.</b> A bank's holdings only mean something alongside the Settlements that draw on it, so a reader
/// handed a bare quantity could add two banks together whose Settlements cannot trade with each other and report a pool that nothing is able to spend.
/// </para>
/// <para>
/// Only Assets whose <c>StorageScope</c> is <c>SettlementBank</c> ever appear here. A player-level or derived figure is held elsewhere entirely - see
/// <see cref="AscendantEmpire.Domain.Enums.Assets.AssetStorageScope"/>.
/// </para>
/// </remarks>
public record AssetBankHoldings
{
    public required int AssetBankId { get; init; }

    /// <summary>
    /// The Player who owns the bank, and the only one who may draw on it.
    /// </summary>
    public required int PlayerId { get; init; }

    /// <summary>
    /// Every Settlement whose membership points at this bank, in ascending id order.
    /// </summary>
    /// <remarks>
    /// Empty is a real state rather than a bug: a bank whose last membership has been repointed by a connection is waiting to be folded into the bank it
    /// joined and deleted, and it still holds its balances until that happens.
    /// </remarks>
    public required IReadOnlyCollection<int> SettlementIds { get; init; }

    /// <summary>
    /// What the bank holds, in the order the Assets are displayed in.
    /// </summary>
    /// <remarks>
    /// An Asset the bank has never held has no entry, and neither does one a drawdown emptied - no row and a zero mean the same thing, so a caller must read
    /// a missing Asset as "none of it" rather than as "unknown".
    /// </remarks>
    public required IReadOnlyCollection<AssetBankHolding> Holdings { get; init; }
}
