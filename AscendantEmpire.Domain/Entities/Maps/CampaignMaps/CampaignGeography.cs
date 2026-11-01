using AscendantEmpire.Domain.Enums.Maps;

using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace AscendantEmpire.Domain.Entities.Maps.CampaignMaps;

public class CampaignGeography
{
    public const int NAME_MAX_LENGTH = 50;
    public const int MAP_EDITOR_ICON_MAX_LENGTH = 50;

    /// <summary>
    /// The <see cref="MovementCost"/> an untraversable Geography carries, as the convention the authored map data follows for "you cannot walk here".
    /// </summary>
    public const decimal IMPASSABLE_MOVEMENT_COST = 999m;

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
    /// Is this Geography water? (i.e. Deep Water, Shallow Water)
    /// </summary>
    public required bool IsWater { get; set; }

    /// <summary>
    /// Is this Geography traversable? (i.e. can a Unit move through it)
    /// </summary>
    public required bool IsTraversable { get; set; }

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
