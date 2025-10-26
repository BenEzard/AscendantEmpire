using AscendantEmpire.Domain.Enums.Maps;
using AscendantEmpire.FrontendContracts.MapGallery.GetCampaignMapGalleryIndex;

namespace AscendantEmpire.Application.Handlers.Maps.MapGallery.Classes;

public class DTCampaignMapGalleryIndex : ICampaignMapGalleryIndex
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
    public MapCreationStatus CreationStatus { get; set; }
    /// <inheritdoc />
    public MapCreationEngine CreationEngine { get; set; }
    /// <inheritdoc />
    public string? PropertiesJson { get; set; }
    /// <inheritdoc />
    public required string Status { get; set; }
    /// <inheritdoc />
    public string? ErrorMessage { get; set; }
}
