using AscendantEmpire.Domain.Enums.Maps;

namespace AscendantEmpire.FrontendContracts.MapGallery.UpdateCampaignMapIndex;

public interface IUpdateCampaignMapIndexRequestDto
{
    public string MapName { get; }
    public string Description { get; }
    public int Width { get; }
    public int Height { get; }
    public MapCreationEngine CreationEngine { get; }
    public Dictionary<string, string> Parameters { get; }
}
