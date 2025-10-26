using AscendantEmpire.Domain.Enums.Maps;

namespace AscendantEmpire.FrontendContracts.MapGallery.GetCampaignMapGalleryIndex;

public interface ICampaignMapGalleryIndex
{
    int CampaignMapGalleryIndexId { get; }
    string MapName { get; }
    string Description { get; }
    int Width { get; }
    int Height { get; }
    MapCreationStatus CreationStatus { get; }
    MapCreationEngine CreationEngine { get; }
    string? PropertiesJson { get; }
    string Status { get; }
    string? ErrorMessage { get; }
}
