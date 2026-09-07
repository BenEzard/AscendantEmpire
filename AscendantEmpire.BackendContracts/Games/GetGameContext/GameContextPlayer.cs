namespace AscendantEmpire.BackendContracts.Games.GetGameContext;

/// <summary>
/// One active Player within a <see cref="GameContext"/>.
/// </summary>
public record GameContextPlayer
{
    public int PlayerId { get; init; }
    public required string RulerTitle { get; init; }
    public required string RulerName { get; init; }
    public required string FactionName { get; init; }
    public required string RulerLabel { get; init; }
    public int CampaignMapOffsetId { get; init; }
}
