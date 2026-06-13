namespace AscendantEmpire.FrontendContracts.MapGallery.UpdateCampaignMapIndex;

public interface IUpdateCampaignMapIndexRequestDto
{
    public string MapName { get; }
    public string? Description { get; }
}
