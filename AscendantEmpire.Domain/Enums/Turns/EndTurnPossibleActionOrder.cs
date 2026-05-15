namespace AscendantEmpire.Domain.Enums.Turns;

/// <summary>
/// At the end of the Turn, we do checks to see if there is anything the Player has missed, which they might want to complete
/// before they end their Turn.
/// These are the possible actions that can be taken at the end of a turn, and the order in which they should be processed.
/// </summary>
public enum EndTurnPossibleActionOrder
{
    // Troop Movements
    TroopsWithRemainingCampaignMovements = 10000,
    TroopsWithRemainingBattleMovements = 10050,

    // Technology
    AreWeResearchingSomething = 20000,

    // Diplomacy
    HaveAnyTradeDealsExpired = 30000,

    // Management


}
