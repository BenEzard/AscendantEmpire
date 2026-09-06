using AscendantEmpire.Domain.Enums.Games;

namespace AscendantEmpire.FrontendContracts.Games.GetGames;

/// <summary>
/// One Game as presented to the System panel's Games page.
/// </summary>
/// <remarks>
/// Both status values are serialised by name rather than by ordinal, so the client mirrors them as string unions.
/// </remarks>
public interface IGameSummary
{
    /// <summary>Database Id of the Game.</summary>
    int GameId { get; }

    /// <summary>Name the Game was created with.</summary>
    string GameName { get; }

    /// <summary>The total number of player slots the Game was set up with.</summary>
    int NumberOfPlayers { get; }

    /// <summary>How many of those slots a user has actually claimed.</summary>
    int ClaimedPlayerCount { get; }

    /// <summary>Whether the Game's data finished being built — the setup status.</summary>
    GameCreationStatus CreationStatus { get; }

    /// <summary>Where the Game has got to in its lifecycle — the game status.</summary>
    GamePlayStatus PlayStatus { get; }

    /// <summary>When the Game was created.</summary>
    DateTimeOffset CreatedAt { get; }
}
