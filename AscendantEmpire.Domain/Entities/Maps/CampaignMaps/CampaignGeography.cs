using AscendantEmpire.Domain.Enums.Maps;

using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace AscendantEmpire.Domain.Entities.Maps.CampaignMaps;

public class CampaignGeography
{
    public const int NAME_MAX_LENGTH = 50;
    public const int MAP_EDITOR_ICON_MAX_LENGTH = 50;

    /// <summary>
    /// Unique identifier for this Campaign Geography
    /// </summary>
    [Key, DatabaseGenerated(DatabaseGeneratedOption.None)]
    public CampaignMapGeographyType CampaignMapGeographyTypeId { get; set; }

    /// <summary>
    /// User-friendly name of this Biome
    /// </summary>
    [MaxLength(NAME_MAX_LENGTH)]
    public string Name { get; set; } = null!;

    /// <summary>
    /// The relative elevation of this Geography.
    /// </summary>
    public int RelativeElevation { get; set; }

    /// <summary>
    /// Movement Cost of this Geography, used to determine how difficult it is to traverse this Tile.
    /// </summary>
    public required decimal MovementCost { get; set; }

    /// <summary>
    /// Icon used in the MapEditor to show the Geography.
    /// </summary>
    [MaxLength(MAP_EDITOR_ICON_MAX_LENGTH)]
    public required string? MapEditorIcon { get; set; }

    /// <summary>
    /// Whether this Geography can be painted on the Campaign Map or not.
    /// </summary>
    public required bool IsPaintableOnCampaignMap { get; set; }
}
