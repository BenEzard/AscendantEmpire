using AscendantEmpire.FrontendContracts.MapGallery.UpdateCampaignMapIndex;

namespace AscendantEmpire.BackendContracts.MapGallery.UpdateCampaignMapIndex;

public class UpdateCampaignMapIndexRequestDto : IUpdateCampaignMapIndexRequestDto
{
    /// <inheritdoc />
    public required string MapName { get; set; }

    /// <inheritdoc />
    public string? Description { get; set; }
}
