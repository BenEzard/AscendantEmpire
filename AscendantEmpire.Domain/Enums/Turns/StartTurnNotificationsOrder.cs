namespace AscendantEmpire.Domain.Enums.Turns;

public enum StartTurnNotificationsOrder
{
    // Military
    SupplyLineCut = 1000,
    SentryAlerts = 1050,

    // Technology

    // Diplomacy
    RumoursOfWar,
    AllyUnderAttack,
    EnemySpyCaught,
    MySpyCaught,


    // Management
    SettlementUnderSiege = 2000,

}
