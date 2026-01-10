namespace AscendantEmpire.FrontendContracts.Games.GetOpenGames;

public interface IOpenGamePlayer
{
    /// <summary>
    /// Unique DB Id for the Player in this Game.
    /// </summary>
    public int PlayerId { get; }

    /// <summary>
    /// Unique Guid for the Player in this Game.
    /// </summary>
    public Guid PlayerGuid { get; }

    /// <summary>
    /// Ruler title (e.g. Emperor)
    /// </summary>
    public string RulerTitle { get; }

    public string RulerName { get; }

    /// <summary>
    /// Name of the faction (e.g. Chinese Empire)
    /// </summary>
    public string FactionName { get; }

    /// <summary>
    /// Concatenation of RulerTitle + " " + RulerName + "(FactionName)"
    /// </summary>
    public string RulerLabel { get; }

    /// <summary>
    /// The offset for the Player Campaign Map (0, 0) position.
    /// </summary>
    public int CampaignOffsetId { get; }
}
