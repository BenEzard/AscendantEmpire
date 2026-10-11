namespace AscendantEmpire.Application.Options;

public class GameSettings
{
    public int MinimumPlayersPerGame { get; set; }
    public int MaximumPlayersPerGame { get; set; }
    public required BattleMapSettings BattleMapSettings { get; set; }
    public required TechnologySettings TechnologySettings { get; set; }
}

public class BattleMapSettings 
{
    public int MinimumBattleMapSize { get; set; }
    public int MaximumBattleMapSize { get; set; }
    public int DefaultBattleMapSize { get; set; }
}

public class TechnologySettings
{
    public int BaseResearchPoints { get; set; }
    public long TechnologySpeedMultiplierPercent { get; set; }
    public long TechnologyFuzzinessPercent { get; set; }
}
