namespace AscendantEmpire.BackendContracts.Assets.GetAssetBanks;

/// <summary>
/// One Settlement's asset bank: what it holds, whose Settlement it is, and which network it sits in.
/// </summary>
/// <remarks>
/// <para>
/// <b>The Settlement travels with the balances deliberately.</b> A bank is one Settlement's store, so a reader handed a bare quantity has no way to say
/// where the stock physically is - which is what per-Settlement capacity and a local-first spend both turn on.
/// </para>
/// <para>
/// <b><see cref="NetworkKey"/> is what says which banks can reach each other.</b> Two banks of one Player carrying the same key may draw on each other;
/// two carrying different keys cannot, however much they add up to.
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
    /// The one Settlement whose store this is.
    /// </summary>
    public required int SettlementId { get; init; }

    /// <summary>
    /// The network the bank belongs to, shared with every other bank its Settlement can draw on.
    /// </summary>
    /// <remarks><see cref="System.Guid.Empty"/> means the bank has not been keyed yet, and so reaches nothing but itself.</remarks>
    public required Guid NetworkKey { get; init; }

    /// <summary>
    /// What the bank holds, in the order the Assets are displayed in.
    /// </summary>
    /// <remarks>
    /// An Asset the bank has never held has no entry, and neither does one a drawdown emptied - no row and a zero mean the same thing, so a caller must read
    /// a missing Asset as "none of it" rather than as "unknown".
    /// </remarks>
    public required IReadOnlyCollection<AssetBankHolding> Holdings { get; init; }
}
