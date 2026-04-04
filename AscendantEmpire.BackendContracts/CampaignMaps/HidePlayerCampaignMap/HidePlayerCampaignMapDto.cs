namespace AscendantEmpire.BackendContracts.CampaignMaps.HidePlayerCampaignMap;

public record HidePlayerCampaignMapDto
{
    public int GameId { get; init; }
    public int PlayerId { get; init; }
}
