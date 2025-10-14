using AscendantEmpire.Domain.Enums.Maps;

namespace AscendantEmpire.FrontendContracts.Geographies.GetCampaignMapGeographies;
public interface ICampaignMapGeography
{
    /// <summary>
    /// Unique identifier for this Campaign Geography
    /// </summary>
    public CampaignMapGeographyType CampaignMapGeographyType { get; }

    /// <summary>
    /// User-friendly name of this Biome
    /// </summary>
    public string Name { get; }

    /// <summary>
    /// The relative elevation of this Geography.
    /// </summary>
    public int RelativeElevation { get; }

    /// <summary>
    /// Movement Cost of this Geography, used to determine how difficult it is to traverse this Tile.
    /// </summary>
    public decimal MovementCost { get; }

    /// <summary>
    /// Icon used in the MapEditor to show the Geography.
    /// </summary>
    public string? MapEditorIcon { get; }

    /// <summary>
    /// Whether this Geography can be painted on the Campaign Map or not.
    /// </summary>
    public bool IsPaintableOnCampaignMap { get; }
}
