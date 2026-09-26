using AscendantEmpire.FrontendContracts.MapGallery.UpdateCampaignMapRivers;

namespace AscendantEmpire.Application.Features.Maps.MapGallery.Classes;

/// <inheritdoc cref="IUpdateCampaignMapRiversResponse" />
public class DTUpdateCampaignMapRiversResponse : IUpdateCampaignMapRiversResponse
{
    /// <inheritdoc />
    public int EdgesUpdated { get; set; }
}
