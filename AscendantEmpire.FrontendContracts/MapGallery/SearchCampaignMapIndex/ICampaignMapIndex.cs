namespace AscendantEmpire.FrontendContracts.MapGallery.SearchCampaignMapIndex;

public interface ICampaignMapIndex
{
    int CampaignMapGalleryIndexId { get; }
    string MapName { get; }
    string Description { get; }
    int Width { get; }
    int Height { get; }
}
