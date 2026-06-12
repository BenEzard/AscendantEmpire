namespace AscendantEmpire.FrontendContracts.Games;

public interface ISetupGame
{
    int MapId { get; }
    string GameName { get; }
    int NumberOfPlayers { get; }
    /// <summary>Width and height (in tiles) of every BattleMap generated in this game.</summary>
    int BattleMapSize { get; }
    /// <summary>Seed for biome diversity in battle maps. Null means generate randomly.</summary>
    long? GenerationSeed { get; }
}
