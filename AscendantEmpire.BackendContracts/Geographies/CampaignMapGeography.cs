using AscendantEmpire.Domain.Enums.Maps;

namespace AscendantEmpire.BackendContracts.Geographies;

public record CampaignMapGeography
{
    /// <summary>
    /// Unique identifier for this Campaign Geography Type.
    /// </summary>
    public required CampaignMapGeographyType CampaignMapGeographyType { get; init; }

    /// <summary>
    /// Elevation value of this Geography.
    /// </summary>
    public required int RelativeElevation { get; init; }

    /// <summary>
    /// Descriptive name of the Geography.
    /// </summary>
    public required string Name { get; init; }

    /// <summary>
    /// Movement Cost of this Geography, used to determine how difficult it is to traverse this Tile.
    /// </summary>
    public required decimal MovementCost { get; init; }

    /// <summary>
    /// Icon used in the MapEditor to show the Geography.
    /// </summary>
    public required string? MapEditorIcon { get; init; }

    /// <summary>
    /// Whether this Geography can be painted on the Campaign Map or not.
    /// </summary>
    public required bool IsPaintableOnCampaignMap { get; init; }
}
