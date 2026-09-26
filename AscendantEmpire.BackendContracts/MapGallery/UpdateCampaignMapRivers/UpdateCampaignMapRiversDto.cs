using AscendantEmpire.FrontendContracts.MapGallery.UpdateCampaignMapRivers;

namespace AscendantEmpire.BackendContracts.MapGallery.UpdateCampaignMapRivers;

/// <inheritdoc cref="IUpdateCampaignMapRiversDto" />
public class UpdateCampaignMapRiversDto : IUpdateCampaignMapRiversDto
{
    /// <inheritdoc />
    public required IReadOnlyCollection<RiverEdgeChange> Changes { get; set; }

    IReadOnlyCollection<IRiverEdgeChange> IUpdateCampaignMapRiversDto.Changes => Changes;
}
