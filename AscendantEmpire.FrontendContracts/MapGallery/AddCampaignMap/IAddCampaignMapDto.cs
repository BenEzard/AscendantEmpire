using AscendantEmpire.Domain.Enums.Maps;

namespace AscendantEmpire.FrontendContracts.MapGallery.GenerateCampaignMap;

public interface IAddCampaignMapDto
{
    /// <summary>
    /// Name of the Map
    /// </summary>
    public string MapName { get; }

    /// <summary>
    /// Description of the Map
    /// </summary>
    public string Description { get; }

    /// <summary>
    /// Width of the Map in Tiles
    /// </summary>
    public int Width { get; }

    /// <summary>
    /// Height of the Map in Tiles
    /// </summary>
    public int Height { get; }

    /// <summary>
    /// Engine to be used for Map Creation
    /// </summary>
    public MapCreationEngine CreationEngine { get; }

    /// <summary>
    /// Additional parameters for Map Generation
    /// </summary>
    public Dictionary<string, string>? Parameters { get; }
}
