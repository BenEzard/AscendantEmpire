namespace AscendantEmpire.Domain.Enums.Assets;

/// <summary>
/// Where the quantity behind an <c>Asset</c> is held, and so what a debit against it has to go looking for.
/// </summary>
/// <remarks>
/// <para>
/// A property of the catalogue row rather than of the bank holding it. Tangible stock moves into a settlement's bank the moment the first settlement is
/// founded, while research and culture points stay player-level for the whole game and population figures are never stored at all - so the scope cannot be a
/// phase of the game or a kind of bank, because the three have to coexist from the first turn onwards.
/// </para>
/// <para>
/// This is what lets the drawdown service route a debit without hardcoding which Assets are not spendable from a bank, and it keeps the
/// <c>Asset</c> row a pure label: where its number comes from is resolved at read time, outside the catalogue.
/// </para>
/// </remarks>
public enum AssetStorageScope
{
    /// <summary>
    /// Held in <c>AssetBankBalance</c>, in the bank of whichever settlement network holds it, and so subject to road and river connectivity.
    /// </summary>
    /// <remarks>The default, and nearly every Asset in the catalogue.</remarks>
    SettlementBank = 1,

    /// <summary>
    /// Held once per Player for the whole game, outside any bank, and unaffected by connectivity.
    /// </summary>
    /// <remarks>
    /// Experience points are the case: <c>PlayerResearchState.UnspentDirectedPoints</c> and <c>UnspentDiffusionPoints</c> already hold them, and
    /// <c>PlayerUnusedExperience</c> the typed-per-kind remainder. An Asset marked this way gets <b>no</b> balance row - a second store would be a second
    /// source of truth for one number, with the research pipeline writing the one the panel is not reading.
    /// </remarks>
    Player = 2,

    /// <summary>
    /// Has no stored balance anywhere. The read path resolves the figure from whatever already records it.
    /// </summary>
    /// <remarks>
    /// The population figures: Civilians from <c>Settlement.Population</c>, the three services from the force rosters, and Slaves / Interned / P.o.W. from
    /// the holdings ledger RS-20 specified and did not build.
    /// </remarks>
    Derived = 3
}
