namespace AscendantEmpire.FrontendContracts.Turns.EndCampaignTurnOrder;

public interface IEndCampaignTurnOutcome
{
    /// <summary>
    /// Whether the Turn actually ended.
    /// </summary>
    /// <remarks>
    /// False is a normal answer, not an error: asking to end a Turn with outstanding actions reports them back in <see cref="PossibleActions"/> and
    /// leaves the Turn running, so the Player can deal with them and then ask again.
    /// </remarks>
    public bool TurnEnded { get; }

    public bool RoundEnded { get; }
    public int? VictoryAchievedByPlayerId { get; }

    /// <summary>
    /// List of Pending Actions which the Player might want to do before the end of turn.
    /// e.g. Move units, select research.
    /// </summary>
    public IReadOnlyCollection<IPossibleAction> PossibleActions { get; }
}
