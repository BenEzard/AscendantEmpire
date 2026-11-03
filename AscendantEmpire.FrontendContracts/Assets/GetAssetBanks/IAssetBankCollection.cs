namespace AscendantEmpire.FrontendContracts.Assets.GetAssetBanks;

/// <summary>
/// Every asset bank a Player holds, which together are everything they own anywhere.
/// </summary>
/// <remarks>
/// A bank per Settlement rather than one merged set of totals, because capacity and locality are both per Settlement. Which of them can reach each other is
/// <see cref="IAssetBankHoldings.NetworkKey"/>: summing banks across two keys advertises stock no single build order can spend. A summary ticker that wants
/// one number adds them up knowing that.
/// </remarks>
public interface IAssetBankCollection
{
    IReadOnlyCollection<IAssetBankHoldings> Banks { get; }
}

/// <summary>
/// Every bank one Settlement can draw on: its own, and any other sharing its network.
/// </summary>
/// <remarks>
/// <see cref="SiteAssetBankId"/> is called out because the site's own bank is spent first and the rest of the network only covers the remainder, so the two
/// are not interchangeable.
/// </remarks>
public interface IAssetNetworkHoldings
{
    /// <summary>
    /// The network these banks share. <see cref="Guid.Empty"/> where the site's bank is in no network at all, in which case it is the only bank returned.
    /// </summary>
    /// <remarks>
    /// Carried here as well as on each bank so a client can tell "the same network, new balances" from "a different network" in one comparison - which is
    /// what a road built or pillaged between turns changes. Every bank in <see cref="Banks"/> carries this same key.
    /// </remarks>
    Guid NetworkKey { get; }

    /// <summary>The bank of the Settlement that was asked about, which is always one of <see cref="Banks"/>.</summary>
    int SiteAssetBankId { get; }

    /// <summary>Every bank in the network, in ascending bank id order. A network of one is the normal case.</summary>
    IReadOnlyCollection<IAssetBankHoldings> Banks { get; }
}

/// <summary>
/// One Settlement's asset bank: what it holds, and which network it sits in.
/// </summary>
public interface IAssetBankHoldings
{
    int AssetBankId { get; }

    /// <summary>
    /// The one Settlement whose store this is.
    /// </summary>
    int SettlementId { get; }

    /// <summary>
    /// The network this bank belongs to. Banks of one Player sharing a key may draw on each other.
    /// </summary>
    Guid NetworkKey { get; }

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
