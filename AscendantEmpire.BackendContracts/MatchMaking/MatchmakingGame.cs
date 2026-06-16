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
public record MatchmakingGame
{
    public int GameId { get; init; }
    public required string GameName { get; init; }
    public int NumberOfPlayers { get; init; }
    public int MapWidth { get; init; }
    public int MapHeight { get; init; }
    public required IReadOnlyCollection<MatchmakingSlot> Slots { get; init; }
}
