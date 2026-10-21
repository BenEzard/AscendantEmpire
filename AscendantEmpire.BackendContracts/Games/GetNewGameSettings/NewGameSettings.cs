namespace AscendantEmpire.BackendContracts.Games.GetNewGameSettings;

public record NewGameSettings
{
    public int MinimumPlayersPerGame { get; init; }
    public int MaximumPlayersPerGame { get; init; }
    
    // Battle Map settings
    public int MinimumBattleMapSize { get; init; }
    public int MaximumBattleMapSize { get; init; }
    public int DefaultBattleMapSize { get; init; }
    public int BattleMapSizeIncrement { get; init; }

    // Technology settings
    public int BaseResearchPoints { get; init; }
    public long TechnologySpeedMultiplierPercent { get; init; }
    public long TechnologyFuzzinessPercent { get; init; }
}
