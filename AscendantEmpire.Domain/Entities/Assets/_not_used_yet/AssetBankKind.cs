namespace AscendantEmpire.Domain.Enums.Assets;

public enum AssetBankKind
{
    /// <summary>
    /// Used before the first settlement and acts as a faction-level stockpile.
    /// </summary>
    FactionStockpile = 0,

    /// <summary>
    /// A live logistics bank shared by one or more connected settlements.
    /// When roads connect, settlements can be reassigned to the same bank.
    /// When cut off, balances can be split into new banks.
    /// </summary>
    SettlementNetwork = 1
}
