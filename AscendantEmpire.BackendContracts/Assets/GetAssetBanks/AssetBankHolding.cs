namespace AscendantEmpire.BackendContracts.Assets.GetAssetBanks;

/// <summary>
/// How much of one Asset a bank holds, with enough of the catalogue row to price an expense against it.
/// </summary>
public record AssetBankHolding
{
    public required int AssetId { get; init; }

    /// <summary>
    /// The Asset's unique code, hierarchical and dotted, e.g. "ConstructionMaterials.Wood.Logs.OakLog".
    /// </summary>
    public required string AssetCode { get; init; }

    public required string DisplayName { get; init; }

    /// <summary>
    /// The group the Asset belongs to, or <see langword="null"/> where it hangs directly off a standalone category.
    /// </summary>
    /// <remarks>
    /// Carried so a caller weighing a group expense can see which of these holdings that group would walk, and in what order, without a second read of the
    /// catalogue. The order of the enclosing collection is the spend order.
    /// </remarks>
    public required int? AssetGroupId { get; init; }

    /// <summary>
    /// The amount held, scaled by <c>ScaledAmount.Scale</c> (1,000).
    /// </summary>
    /// <remarks>
    /// Raw rather than whole because this is the figure a drawdown compares an expense against, and rounding it here would let a bank holding 0.4 Oak Logs
    /// read as empty to one caller and as sufficient to another. Convert at the edge: the UI and the API still speak whole numbers.
    /// </remarks>
    public required long RawQuantity { get; init; }

    /// <summary>
    /// How much of the amount held is in a strategic reserve, scaled by <c>ScaledAmount.Scale</c> (1,000).
    /// </summary>
    /// <remarks>
    /// <b>Not part of <see cref="RawQuantity"/>, and not spendable.</b> It is carried so that a caller can tell a bank holding nothing but a reserve from an
    /// empty one, and so the ticker can say an Asset has a reserve at all; a drawdown still prices an expense against <see cref="RawQuantity"/> alone.
    /// </remarks>
    public required long RawReserved { get; init; }
}
