namespace AscendantEmpire.FrontendContracts.Games.GetOpenGames;

public interface IOpenGame
{
    /// <summary>
    /// Database Id of the Game
    /// </summary>
    int GameId { get; }

    /// <summary>
    /// Name of the Game
    /// </summary>
    string GameName { get; }

    /// <summary>
    /// Unique Guid of the Game
    /// </summary>
    Guid GameGuid { get; }

    /// <summary>
    /// Map Width
    /// </summary>
    int MapWidth { get; }

    /// <summary>
    /// Map Height
    /// </summary>
    int MapHeight { get; }

    /// <summary>
    /// List of Players who are still active in the Game
    /// </summary>
    IReadOnlyCollection<IOpenGamePlayer> Players { get; }
}
