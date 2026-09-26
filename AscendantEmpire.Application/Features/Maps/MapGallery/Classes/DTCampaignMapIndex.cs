using AscendantEmpire.FrontendContracts.MapGallery.SearchCampaignMapIndex;

namespace AscendantEmpire.Application.Features.Maps.MapGallery.Classes;

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
    /// <inheritdoc />
    public bool HorizontalWrap { get; set; }
    /// <inheritdoc />
    public bool VerticalWrap { get; set; }
}
