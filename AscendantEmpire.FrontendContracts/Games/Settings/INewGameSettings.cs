namespace AscendantEmpire.FrontendContracts.Games.Settings;

public interface INewGameSettings
{
    int MinimumPlayersPerGame { get; }
    int MaximumPlayersPerGame { get; }
    IBattleMapSettings BattleMapSettings { get; }
    ITechnologySettings TechnologySettings { get; }
}

public interface IBattleMapSettings
{
    int MinimumBattleMapSize { get; }
    int MaximumBattleMapSize { get; }
    int DefaultBattleMapSize { get; }
}

public interface ITechnologySettings
{
    int BaseResearchPoints { get; }
    long TechnologySpeedMultiplierPercent { get; }
    long TechnologyFuzzinessPercent { get; }
}