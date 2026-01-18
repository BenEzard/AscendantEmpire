namespace AscendantEmpire.BackendContracts.CampaignMaps.GetPlayerCampaignMap;

public record GetPlayerCampaignMapDto
{
    public Guid GameGuid { get; init; }
    public Guid PlayerGuid { get; init; }
    public bool LoadWholeMap { get; init; }
    public IReadOnlyCollection<MapObserver>? Observers { get; init; } = null;
}
