namespace AscendantEmpire.BackendContracts.CampaignMap;

public record GetPlayerCampaignMapDto
{
    public Guid GameGuid { get; init; }
    public Guid PlayerGuid { get; init; }
    public IReadOnlyCollection<MapObserver>? Observers { get; init; } = null;
}
