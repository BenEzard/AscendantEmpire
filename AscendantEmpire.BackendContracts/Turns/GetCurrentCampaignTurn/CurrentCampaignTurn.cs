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
}