namespace AscendantEmpire.FrontendContracts.Turns.GetTurns;

public interface IRosterEntry
{
    int CurrentTurnPlayerId { get; }
    int NumberOfPlayersUntilMyTurn { get; }
    bool IsCampaign { get; }
    TimeSpan MaxTimeRemainingOnTurn { get; }
    int RoundNumber { get; }
}
