using AscendantEmpire.FrontendContracts.Games;

namespace AscendantEmpire.BackendContracts.Games.AddGame;

public record class SetupGameDto : ISetupGame
{
    public required int MapId { get; init; }
    public required string GameName { get; init; }
    public required int NumberOfPlayers { get; init; }
    public required int BattleMapSize { get; init; }
    /// <summary>Leave null to have the server generate a random seed.</summary>
    public long? GenerationSeed { get; init; }
}
