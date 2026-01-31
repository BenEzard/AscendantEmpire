
using AscendantEmpire.FrontendContracts.Turns.GetTurns;

namespace AscendantEmpire.Application.Handlers.Turns.Classes;

public class DTRosterEntry : IRosterEntry
{
    public int CurrentTurnPlayerId { get; set; }

    public int NumberOfPlayersUntilMyTurn { get; set; }

    public bool IsCampaign { get; set; }

    public TimeSpan MaxTimeRemainingOnTurn { get; set; }
    public int RoundNumber { get; set; }
}
