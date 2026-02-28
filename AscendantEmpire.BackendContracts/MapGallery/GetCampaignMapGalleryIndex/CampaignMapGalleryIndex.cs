using AscendantEmpire.Domain.Enums.Maps;
using AscendantEmpire.FrontendContracts.MapGallery.GetCampaignMapGalleryIndex;

namespace AscendantEmpire.BackendContracts.MapGallery.GetCampaignMapGalleryIndex;

public record CampaignMapGalleryIndex : ICampaignMapGalleryIndex
{
    public required int CampaignMapGalleryIndexId { get; init; }
    public required string MapName { get; init; }
    public required string Description { get; init; }
    public required int Width { get; init; }
    public required int Height { get; init; }
    public required int CampaignMapOffsetId { get; init; }
    public required MapCreationStatus CreationStatus { get; init; }
    public required MapCreationEngine CreationEngine { get; init; }
    public required string Status { get; init; }
    public required string ErrorMessage { get; init; }
    public string? PropertiesJson { get; init; }
}
