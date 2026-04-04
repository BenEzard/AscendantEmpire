namespace AscendantEmpire.BackendContracts.CampaignMaps.GetPlayerCampaignMap;

public record GetPlayerCampaignMapDto
{
    public int GameId { get; init; }
    public int PlayerId { get; init; }
    public bool LoadWholeMap { get; init; }
}
