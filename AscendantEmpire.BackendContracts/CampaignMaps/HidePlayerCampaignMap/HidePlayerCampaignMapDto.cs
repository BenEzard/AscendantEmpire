namespace AscendantEmpire.BackendContracts.CampaignMaps.HidePlayerCampaignMap;

public record HidePlayerCampaignMapDto
{
    public Guid GameGuid { get; init; }
    public Guid PlayerGuid { get; init; }
}
