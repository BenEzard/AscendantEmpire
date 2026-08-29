namespace AscendantEmpire.BackendContracts.Games.GetOpenGames;

public record OpenGame
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
    /// counterpart of <see cref="OpenGamePlayer.CampaignMapOffsetId"/>, which locates one player's
    /// block in the per-player map; troops and structures are stored against the world map, so
    /// translating between the two needs both.
    /// </remarks>
    public int CampaignMapOffsetId { get; init; }
    public bool HasHorizontalWrapping { get; init; }
    public bool HasVerticalWrapping { get; init; }
    public required IReadOnlyCollection<OpenGamePlayer> Players { get; init; }
}