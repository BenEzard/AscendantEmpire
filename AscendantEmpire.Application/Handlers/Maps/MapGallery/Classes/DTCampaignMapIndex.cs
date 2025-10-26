using AscendantEmpire.BackendContracts.MapGallery.SearchCampaignMap;
using AscendantEmpire.FrontendContracts.MapGallery.SearchCampaignMapIndex;

namespace AscendantEmpire.Application.Handlers.Maps.MapGallery.Classes;

public class DTCampaignMapIndex : ICampaignMapIndex
{
    /// <inheritdoc />
    public int CampaignMapGalleryIndexId { get; set; }
    /// <inheritdoc />
    public required string MapName { get; set; }
    /// <inheritdoc />
    public required string Description { get; set; }
    /// <inheritdoc />
    public int Width { get; set; }
    /// <inheritdoc />
    public int Height { get; set; }
}
