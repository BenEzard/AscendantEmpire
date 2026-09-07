namespace AscendantEmpire.BackendContracts.Games.GetMyGames;

/// <summary>
/// One in-progress Game that the calling User holds an active Player slot in.
/// </summary>
/// <remarks>
/// <para>
/// This is a personal listing, not a lobby: it carries one row per Game rather than one per Player, and it only ever describes Games the caller is
/// actually in. Two players' listings therefore differ, and neither shows the other's games.
/// </para>
/// <para>
/// Two Players are named on each row and they serve different purposes. <see cref="YourPlayerId"/> and friends are the caller's own slot, used to draw
/// the board once the Game is entered. The <c>CurrentTurn</c> fields are display detail about whoever the campaign queue is currently waiting on, which
/// may well be somebody else.
/// </para>
/// </remarks>
public record MyGame
{
    public int GameId { get; init; }
    public required string GameName { get; init; }
    public int MapWidth { get; init; }
    public int MapHeight { get; init; }

    /// <summary>
    /// The caller's own Player slot in this Game.
    /// </summary>
    public int YourPlayerId { get; init; }

    /// <summary>
    /// The caller's own ruler, as <c>"{RulerTitle} {RulerName} ({FactionName})"</c>.
    /// </summary>
    public required string YourRulerLabel { get; init; }

    /// <summary>
    /// The offset for the caller's Player Campaign Map (0, 0) position.
    /// </summary>
    public int YourCampaignOffsetId { get; init; }

    /// <summary>
    /// The Player whose campaign turn it currently is, or null when nobody is taking one.
    /// </summary>
    /// <remarks>
    /// Null is a normal state, not a fault. A Game sitting between turns, and one whose campaign queue has not started yet, both have no participant
    /// taking a turn, and both must still appear in the listing.
    /// </remarks>
    public int? CurrentTurnPlayerId { get; init; }

    /// <summary>
    /// The current-turn ruler, as <c>"{RulerTitle} {RulerName} ({FactionName})"</c>, or null alongside a null <see cref="CurrentTurnPlayerId"/>.
    /// </summary>
    public string? CurrentTurnRulerLabel { get; init; }

    /// <summary>
    /// Whether the current campaign turn is the caller's own.
    /// </summary>
    public bool IsYourTurn { get; init; }
}
