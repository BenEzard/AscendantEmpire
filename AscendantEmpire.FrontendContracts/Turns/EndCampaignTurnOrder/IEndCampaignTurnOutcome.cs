namespace AscendantEmpire.FrontendContracts.Turns.EndCampaignTurnOrder;

public interface IEndCampaignTurnOutcome
{
    public bool TurnEnded { get; }
    public bool RoundEnded { get; }
    public int? VictoryAchievedByPlayerId { get; }

    /// <summary>
    /// List of Pending Actions which the Player might want to do before the end of turn.
    /// e.g. Move units, select research.
    /// </summary>
    public List<IPossibleAction> PossibleActions { get; }
}
