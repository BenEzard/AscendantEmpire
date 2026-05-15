namespace AscendantEmpire.BackendContracts.Turns.GetCampaignRoundInfo;

public record class CampaignRound
{
    public int GameId { get; init; }
    public int TurnQueueId { get; init; }

    public int CurrentRoundNumber { get; init; }

    public int? CurrentPlayerId { get; init; }
    public int? CurrentPlayerTurnOrder { get; init; }

    public DateTimeOffset? TurnStartTime { get; init; }
    public DateTimeOffset? ScheduledTurnEndTime { get; init; }
    public DateTimeOffset? ActualTurnEndTime { get; init; }

    public bool IsCompleted { get; init; }
}
