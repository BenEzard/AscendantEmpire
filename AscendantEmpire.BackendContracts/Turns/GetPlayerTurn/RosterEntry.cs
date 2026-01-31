namespace AscendantEmpire.BackendContracts.Turns.GetPlayerTurn;

public record RosterEntry
{
    public int CurrentTurnPlayerId { get; init; }
    public int NumberOfPlayersUntilMyTurn { get; init; }
    public bool IsCampaign { get; init; }
    public TimeSpan MaxTimeRemainingUntilTurn { get; init; }
    public int RoundNumber { get; init; }
}
