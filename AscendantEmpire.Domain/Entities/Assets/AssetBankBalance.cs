namespace AscendantEmpire.Domain.Entities.Assets;

/// <summary>
/// How much of one Asset a given <see cref="AssetBank"/> currently holds.
/// </summary>
/// <remarks>
/// <para>
/// Composite primary key on (AssetBankId, AssetId), so a bank holds at most one balance per Asset and a drawdown has a single row to debit rather than a set
/// to reconcile.
/// </para>
/// <para>
/// <b>No row means none held.</b> A balance is not created until something is first credited to it, and a debit that would empty one may delete it rather
/// than leave a zero behind - neither a missing row nor a zero means anything different to a reader.
/// </para>
/// <para>
/// Only an Asset whose <c>StorageScope</c> is <c>SettlementBank</c> ever appears here. A player-level or derived Asset has its figure elsewhere entirely -
/// see <see cref="Enums.Assets.AssetStorageScope"/>.
/// </para>
/// </remarks>
public sealed class AssetBankBalance
{
    public int AssetBankId { get; set; }

    public int AssetId { get; set; }

    /// <summary>
    /// The amount held, stored scaled by <c>ScaledAmount.Scale</c> (1,000). Never negative.
    /// </summary>
    /// <remarks>
    /// Scaled rather than a whole count so that a fractional per-turn yield accumulates without being rounded away at the balance - a tile giving 2.5 stone a
    /// turn has to land somewhere. Convert with <c>ScaledResourceValue</c> at the edges; the UI and the API still speak whole numbers.
    /// </remarks>
    public long Quantity { get; set; }

    public AssetBank Bank { get; set; } = null!;

    public Asset Asset { get; set; } = null!;
}
