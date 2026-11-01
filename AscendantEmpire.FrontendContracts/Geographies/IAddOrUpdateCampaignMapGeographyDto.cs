using AscendantEmpire.Domain.Enums.Maps;

namespace AscendantEmpire.FrontendContracts.Geographies;

public interface IAddOrUpdateCampaignMapGeographyDto
{
    public CampaignMapGeographyType CampaignMapGeographyType { get; }

    /// <summary>
    /// Elevation value of this Geography.
    /// </summary>
    int RelativeElevation { get; }

    /// <summary>
    /// Descriptive name of the Geography.
    /// </summary>
    string Name { get; }

    /// <summary>
    /// Movement Cost of this Geography, used to determine how difficult it is to traverse this Tile.
    /// </summary>
    decimal MovementCost { get; }

    /// <summary>
    /// Whether this Geography is water, e.g. Deep Water or Shallow Water.
    /// </summary>
    bool IsWater { get; }

    /// <summary>
    /// Whether anything can enter this Geography at all. False for Mountainous.
    /// </summary>
    bool IsTraversable { get; }

    /// <summary>
    /// Icon used in the MapEditor to show the Geography.
    /// </summary>
    string? MapEditorIcon { get; }

    /// <summary>
    /// Whether this Geography can be painted on the Campaign Map or not.
    /// </summary>
    bool IsPaintableOnCampaignMap { get; }
}
