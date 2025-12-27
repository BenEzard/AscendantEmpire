using AscendantEmpire.FrontendContracts.Games;

namespace AscendantEmpire.BackendContracts.Game.AddGame;

public record class SetupGameDto : ISetupGame
{
    public required int MapId { get; init; }
    public required string GameName { get; init; }
    public required int NumberOfPlayers { get; init; }
}
