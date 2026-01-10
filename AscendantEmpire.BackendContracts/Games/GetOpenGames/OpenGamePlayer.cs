namespace AscendantEmpire.BackendContracts.Games.GetOpenGames;

public record OpenGamePlayer
{
    public Guid PlayerGuid { get; init; }
    public int PlayerId { get; init; }
    public required string RulerTitle { get; init; }
    public required string RulerName { get; init; }
    public required string FactionName { get; init; }
    public required string RulerLabel { get; init; }
    public int CampaignMapOffsetId { get; init; }
}