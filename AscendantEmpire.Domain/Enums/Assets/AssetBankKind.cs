namespace AscendantEmpire.Domain.Enums.Assets;

/// <summary>
/// What an <c>AssetBank</c> is the store of, and so whether its contents are part of the Empire's holdings.
/// </summary>
/// <remarks>
/// <para>
/// <b>The discriminator exists for the exclusion rule.</b> Every Empire-wide read and every Empire-wide expense sums over the banks a Player owns, and a
/// raiding party's loot is not stock the Empire can spend - a technology funded out of it would be funded out of goods that are still in a saddlebag on
/// somebody else's territory. Filtering on this is what keeps the two apart, and it is why the column is stored rather than inferred from which of
/// <c>AssetBank.SettlementId</c> and <c>AssetBank.UnitInstanceId</c> happens to be set.
/// </para>
/// <para>
/// It is not a kind of storage and not a storage scope. Three different mechanisms keep a quantity out of "what I have" and they are not interchangeable:
/// the strategic reserve is a different column, force cargo is this discriminator, and a Player-held or derived Asset is a different
/// <see cref="AssetStorageScope"/> entirely.
/// </para>
/// </remarks>
public enum AssetBankKind
{
    /// <summary>
    /// One Settlement's store, reachable by every bank sharing its network, and part of the Empire's holdings.
    /// </summary>
    /// <remarks>Every bank written before the discriminator existed is one of these, which is what the column's backfill says.</remarks>
    Settlement = 1,

    /// <summary>
    /// One Unit's cargo - what it is carrying - which is excluded from Empire totals and from Empire-wide spending until it is deposited.
    /// </summary>
    /// <remarks>
    /// Attached to a <c>UnitInstance</c> rather than to a Force, because Force is a category and not a type: a Force is either a Unit or a Command, with no
    /// shared table for a single foreign key to hang on. A Command's pool is the network of its Units' banks.
    /// </remarks>
    Unit = 2
}
