namespace AscendantEmpire.FrontendContracts.Assets.GetAssetBanks;

/// <summary>
/// Every asset bank a Player holds, which together are everything they own anywhere.
/// </summary>
/// <remarks>
/// A list of banks rather than one merged set of totals. Two banks are two pools precisely because their Settlements cannot reach each other, so summing
/// them into a single figure would advertise stock that no single build order can spend. A summary ticker that wants one number adds them up knowing that.
/// </remarks>
public interface IAssetBankCollection
{
    IReadOnlyCollection<IAssetBankHoldings> Banks { get; }
}

/// <summary>
/// One asset bank: what it holds, and which Settlements draw on it.
/// </summary>
public interface IAssetBankHoldings
{
    int AssetBankId { get; }

    /// <summary>
    /// Every Settlement whose membership points at this bank, in ascending id order.
    /// </summary>
    IReadOnlyCollection<int> SettlementIds { get; }

    /// <summary>
    /// What the bank holds, in display order. An Asset with no entry is one the bank holds none of.
    /// </summary>
    IReadOnlyCollection<IAssetBankHolding> Holdings { get; }
}

/// <summary>
/// How much of one Asset a bank holds.
/// </summary>
public interface IAssetBankHolding
{
    int AssetId { get; }

    /// <summary>
    /// The Asset's unique code, e.g. "ConstructionMaterials.Wood.Logs.OakLog".
    /// </summary>
    string AssetCode { get; }

    string DisplayName { get; }

    /// <summary>
    /// The group the Asset belongs to, or <see langword="null"/> where it hangs directly off a standalone category.
    /// </summary>
    int? AssetGroupId { get; }

    /// <summary>
    /// The amount held, as a whole number, rounded down.
    /// </summary>
    /// <remarks>
    /// Whole because that is what the UI shows everywhere else. The stored figure is fractional, so a bank holding nine tenths of a log reads here as none -
    /// which is also what the drawdown would let the Player spend of it.
    /// </remarks>
    long Quantity { get; }
}
