namespace AscendantEmpire.BackendContracts.Games.GetOpenGames;

public record OpenGame
{
    public int GameId { get; init; }
    public required string GameName { get; init; }
    public int NumberOfPlayers { get; init; }
    public int MapWidth { get; init; }
    public int MapHeight { get; init; }
    public bool HasHorizontalWrapping { get; init; }
    public bool HasVerticalWrapping { get; init; }
    public required IReadOnlyCollection<OpenGamePlayer> Players { get; init; }
}