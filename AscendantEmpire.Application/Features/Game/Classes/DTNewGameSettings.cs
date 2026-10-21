using AscendantEmpire.FrontendContracts.Games.Settings;

namespace AscendantEmpire.Application.Features.Game.Classes;

public class DTNewGameSettings : INewGameSettings
{
    public int MinimumPlayersPerGame { get; set; }

    public int MaximumPlayersPerGame { get; set; }

    public required IBattleMapSettings BattleMapSettings { get; set; }

    public required ITechnologySettings TechnologySettings { get; set; }
}

public class DTBattleMapSettings : IBattleMapSettings
{
    public int MinimumBattleMapSize { get; set; }
    public int MaximumBattleMapSize { get; set; }
    public int DefaultBattleMapSize { get; set; }
    public int BattleMapSizeIncrement { get; set; }
}

public class DTTechnologySettings : ITechnologySettings
{
    public int BaseResearchPoints { get; set; }
    public long TechnologySpeedMultiplierPercent { get; set; }
    public long TechnologyFuzzinessPercent { get; set; }
}
