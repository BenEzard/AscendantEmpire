namespace AscendantEmpire.FrontendContracts.MatchMaking;

/// <summary>
/// A joinable game and its player slots, as presented to the matchmaker UI.
/// </summary>
public interface IMatchMakingGame
{
    /// <summary>Database Id of the Game.</summary>
    int GameId { get; }

    /// <summary>Name of the Game.</summary>
    string GameName { get; }

    /// <summary>The total number of player slots the game was set up with.</summary>
    int NumberOfPlayers { get; }

    /// <summary>Map width in tiles.</summary>
    int MapWidth { get; }

    /// <summary>Map height in tiles.</summary>
    int MapHeight { get; }

    /// <summary>All active slots in the game, claimed and unclaimed alike.</summary>
    IReadOnlyCollection<IMatchMakingSlot> Slots { get; }

    /// <summary>
    /// True once the game is under way, which is the only state entering it is allowed from.
    /// </summary>
    /// <remarks>
    /// A game starts when every active slot has been claimed <i>and</i> every one of those players has defined themselves, so a lobby can be full and
    /// still not started. <see cref="IMatchMakingSlot.IsDefined"/> is what tells those two waits apart.
    /// </remarks>
    bool HasStarted { get; }
}
