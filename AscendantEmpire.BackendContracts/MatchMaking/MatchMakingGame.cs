namespace AscendantEmpire.BackendContracts.MatchMaking;

/// <summary>
/// A joinable game and its player slots, as seen by a specific user during matchmaking.
/// </summary>
/// <remarks>
/// <para>
/// Unlike the anonymous open-games listing, this view includes every active slot — claimed or
/// not — so the matchmaker can show which seats are taken and which are still free.
/// </para>
/// </remarks>
public record MatchMakingGame
{
    public int GameId { get; init; }
    public required string GameName { get; init; }
    public int NumberOfPlayers { get; init; }
    public int MapWidth { get; init; }
    public int MapHeight { get; init; }

    /// <summary>
    /// Whether play has begun, which it does the moment the last free slot is claimed.
    /// </summary>
    /// <remarks>
    /// A game that has not started cannot be entered - the API refuses it - so the client uses this to keep the Play button out of reach
    /// while the lobby is still filling, rather than offering a button that can only fail.
    /// </remarks>
    public bool HasStarted { get; init; }

    public required IReadOnlyCollection<MatchMakingSlot> Slots { get; init; }
}
