namespace AscendantEmpire.FrontendContracts.Games.GetMyGames;

/// <summary>
/// One of the caller's own in-progress games, as presented to the Open Game dialog.
/// </summary>
public interface IMyGame
{
    /// <summary>Database Id of the Game.</summary>
    int GameId { get; }

    /// <summary>Name of the Game.</summary>
    string GameName { get; }

    /// <summary>Map width in tiles.</summary>
    int MapWidth { get; }

    /// <summary>Map height in tiles.</summary>
    int MapHeight { get; }

    /// <summary>Unique DB Id of the caller's own Player in this Game.</summary>
    int YourPlayerId { get; }

    /// <summary>The caller's own ruler, as RulerTitle + " " + RulerName + " (FactionName)".</summary>
    string YourRulerLabel { get; }

    /// <summary>The offset for the caller's Player Campaign Map (0, 0) position.</summary>
    int YourCampaignOffsetId { get; }

    /// <summary>
    /// Unique DB Id of the Player whose campaign turn it currently is, or null when nobody is taking one.
    /// </summary>
    /// <remarks>
    /// Null covers both a game between turns and a game whose campaign queue has not started; the client shows a placeholder rather than a ruler.
    /// </remarks>
    int? CurrentTurnPlayerId { get; }

    /// <summary>The current-turn ruler, as RulerTitle + " " + RulerName + " (FactionName)", or null when there is no current turn.</summary>
    string? CurrentTurnRulerLabel { get; }

    /// <summary>Whether the current campaign turn is the caller's own.</summary>
    bool IsYourTurn { get; }
}
