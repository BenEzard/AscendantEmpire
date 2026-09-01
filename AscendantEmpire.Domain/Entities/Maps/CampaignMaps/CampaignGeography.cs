using AscendantEmpire.Domain.Enums.Maps;

using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace AscendantEmpire.Domain.Entities.Maps.CampaignMaps;

public class CampaignGeography
{
    public const int NAME_MAX_LENGTH = 50;
    public const int MAP_EDITOR_ICON_MAX_LENGTH = 50;

    /// <summary>
    /// The <see cref="MovementCost"/> at or above which a Geography is treated as impassable rather than merely expensive.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Mountainous is seeded at exactly this value, and it is the convention the authored map data follows for "you cannot walk here".
    /// </para>
    /// <para>
    /// This gates where a start position may be placed, and which Geographies may be painted over an existing one.
    /// </para>
    /// </remarks>
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
    /// Icon used in the MapEditor to show the Geography.
    /// </summary>
    [MaxLength(MAP_EDITOR_ICON_MAX_LENGTH)]
    public required string? MapEditorIcon { get; set; }

    /// <summary>
    /// Whether this Geography can be painted on the Campaign Map or not.
    /// </summary>
    public required bool IsPaintableOnCampaignMap { get; set; }
}
