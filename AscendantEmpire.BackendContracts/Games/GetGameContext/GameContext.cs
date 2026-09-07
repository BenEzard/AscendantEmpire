namespace AscendantEmpire.BackendContracts.Games.GetGameContext;

/// <summary>
/// A Game's map geometry together with the campaign offsets of its active Players.
/// </summary>
/// <remarks>
/// <para>
/// This is an internal lookup rather than anything a user sees. Handlers use it to translate between world-map and per-player tile ids, to read the map's
/// dimensions and wrapping, and as a cheap check that a Player really belongs to an in-progress Game.
/// </para>
/// </remarks>
public record GameContext
{
    public int GameId { get; init; }
    public required string GameName { get; init; }
    public int NumberOfPlayers { get; init; }
    public int MapWidth { get; init; }
    public int MapHeight { get; init; }

    /// <summary>
    /// The world-map tile id of this game's tile at coordinate (0, 0).
    /// </summary>
    /// <remarks>
    /// A tile's id is <c>CampaignMapOffsetId + XCoord + YCoord * MapWidth</c>. This is the game-wide
    /// counterpart of <see cref="GameContextPlayer.CampaignMapOffsetId"/>, which locates one player's
    /// block in the per-player map; Units and structures are stored against the world map, so
    /// translating between the two needs both.
    /// </remarks>
    public int CampaignMapOffsetId { get; init; }
    public bool HasHorizontalWrapping { get; init; }
    public bool HasVerticalWrapping { get; init; }
    public required IReadOnlyCollection<GameContextPlayer> Players { get; init; }
}
