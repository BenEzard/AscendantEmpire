namespace AscendantEmpire.BackendContracts.Turns.GetCurrentCampaignTurn;

public record class CurrentCampaignTurn
{
    public int GameId { get; set; }
    public int TurnQueueParticipantId { get; init; }
    public int TurnQueueId { get; init; }
    public int PlayerId { get; init; }
    public int TurnOrder { get; init; }
    public bool IsLastTurnInRound { get; init; }
    public int CurrentRoundNumber { get; init; }
    public string FactionName { get; init; } = string.Empty;

    /// <summary>
    /// The open <c>turns.PlayerTurn</c> row for this Turn, when there is one.
    /// </summary>
    /// <remarks>
    /// Populated only by the "current" lookup, which describes a Turn that is being taken. The "next" and "first" lookups describe a Turn that has not
    /// started - the row does not exist until the Turn opens - so they leave this null, and the service that opens the Turn fills it in.
    /// </remarks>
    public int? PlayerTurnId { get; init; }
}