namespace AscendantEmpire.FrontendContracts.Games;

public interface ISetupGame
{
    int MapId { get; }
    string GameName { get; }
    int NumberOfPlayers { get; }
}
