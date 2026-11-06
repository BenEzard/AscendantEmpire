namespace AscendantEmpire.Domain.Entities.Assets;

/// <summary>
/// How much of one Asset one Settlement's <see cref="AssetBank"/> currently holds.
/// </summary>
/// <remarks>
/// <para>
/// Composite primary key on (AssetBankId, AssetId), so a bank holds at most one balance per Asset and a drawdown has a single row to debit rather than a set
/// to reconcile.
/// </para>
/// <para>
/// <b>A balance never moves to another bank.</b> Connecting and disconnecting Settlements re-key <see cref="AssetBank.NetworkKey"/> and touch no row here, so
/// stock stays with the Settlement that produced it. The one sanctioned exception anywhere in the model is a camp delivery (RS-35).
/// </para>
/// <para>
/// <b>No row means none held.</b> A balance is not created until something is first credited to it, and a debit that would empty one may delete it rather
/// than leave a zero behind - neither a missing row nor a zero means anything different to a reader. A row is only empty when
/// <see cref="Quantity"/> and <see cref="ReservedQuantity"/> are <i>both</i> zero: a bank holding nothing but a strategic reserve still has to keep its row.
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

    /// <summary>
    /// How much of the amount held is in a strategic reserve, stored scaled by <c>ScaledAmount.Scale</c> (1,000). Never negative.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Beside <see cref="Quantity"/> rather than part of it.</b> A reserved quantity is out of circulation and out of every total until it is released, and
    /// a second column is what makes that free: <c>PlayerAssetQuantityReader</c>, the ticker and both arms of the drawdown all read <see cref="Quantity"/>, so
    /// none of them can accidentally spend or report a reserve and none of them needed a filter adding.
    /// </para>
    /// <para>
    /// It also sits outside the Settlement's stockpile - its dedicated plus generic storage - because the Strategic Reserve building grants a budget of its
    /// own. <c>AssetBankCapacityService</c> sums only <see cref="Quantity"/>, which is why a reserve cannot strangle production by eating the working store.
    /// </para>
    /// <para>
    /// <b>Released</b> is the single act that moves an amount back into <see cref="Quantity"/> and so into totals. See <c>docs/Terminology.md</c>.
    /// </para>
    /// </remarks>
    public long ReservedQuantity { get; set; }

    public AssetBank Bank { get; set; } = null!;

    public Asset Asset { get; set; } = null!;
}
