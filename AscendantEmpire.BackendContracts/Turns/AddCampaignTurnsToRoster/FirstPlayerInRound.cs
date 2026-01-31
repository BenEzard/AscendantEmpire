namespace AscendantEmpire.BackendContracts.Turns.AddCampaignTurnsToRoster;

public record FirstPlayerInRound
{
    public Guid PlayerGuid { get; init; }
    public int RoundNumber { get; init; }
}
