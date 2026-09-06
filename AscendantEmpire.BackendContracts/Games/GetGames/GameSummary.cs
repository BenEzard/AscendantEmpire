using AscendantEmpire.Domain.Enums.Games;

namespace AscendantEmpire.BackendContracts.Games.GetGames;

/// <summary>
/// One row of the System panel's administrative games listing.
/// </summary>
/// <remarks>
/// <para>
/// Unlike the matchmaking and open-game listings, this one is deliberately unfiltered: it returns every Game whatever its creation or play status. The page
/// it feeds exists to delete games that have gone wrong — a setup that failed half way through, or a game nobody is ever going to finish — and those are
/// exactly the games the other listings hide.
/// </para>
/// <para>
/// It carries no player detail. The page only needs to identify a game and say roughly where it has got to.
/// </para>
/// </remarks>
public record GameSummary
{
    /// <summary>Database Id of the Game.</summary>
    public int GameId { get; init; }

    /// <summary>Name the Game was created with.</summary>
    public required string GameName { get; init; }

    /// <summary>The total number of player slots the Game was set up with.</summary>
    public int NumberOfPlayers { get; init; }

    /// <summary>How many of those slots a user has actually claimed.</summary>
    public int ClaimedPlayerCount { get; init; }

    /// <summary>Whether the Game's data finished being built.</summary>
    public GameCreationStatus CreationStatus { get; init; }

    /// <summary>Where the Game has got to in its lifecycle.</summary>
    public GamePlayStatus PlayStatus { get; init; }

    /// <summary>When the Game was created.</summary>
    public DateTimeOffset CreatedAt { get; init; }
}
