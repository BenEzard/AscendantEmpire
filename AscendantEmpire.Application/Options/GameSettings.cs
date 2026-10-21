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

    /// <summary>
    /// The step a battle map size must land on: every allowed size is a multiple of this.
    /// </summary>
    /// <remarks>
    /// Configured rather than fixed in code because it is the same kind of rule as the bounds beside it, and the New Game dialog builds its slider
    /// from all four together. Keep it a factor of both bounds, or the slider's top notch will be a size the save then refuses.
    /// </remarks>
    public int BattleMapSizeIncrement { get; set; }
}

public class TechnologySettings
{
    public int BaseResearchPoints { get; set; }
    public long TechnologySpeedMultiplierPercent { get; set; }
    public long TechnologyFuzzinessPercent { get; set; }
}
