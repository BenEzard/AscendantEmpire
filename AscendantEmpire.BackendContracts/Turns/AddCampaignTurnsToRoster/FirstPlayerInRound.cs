namespace AscendantEmpire.BackendContracts.Turns.AddCampaignTurnsToRoster;

public record FirstPlayerInRound
{
    public int PlayerId { get; init; }
    public int RoundNumber { get; init; }
}
