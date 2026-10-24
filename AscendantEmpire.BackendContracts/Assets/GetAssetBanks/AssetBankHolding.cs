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
    /// The amount held, scaled by <c>ScaledAmount.Scale</c> (1,000), exactly as the column stores it.
    /// </summary>
    /// <remarks>
    /// Raw rather than whole because this is the figure a drawdown compares an expense against, and rounding it here would let a bank holding 0.4 Oak Logs
    /// read as empty to one caller and as sufficient to another. Convert at the edge: the UI and the API still speak whole numbers.
    /// </remarks>
    public required long RawQuantity { get; init; }
}
