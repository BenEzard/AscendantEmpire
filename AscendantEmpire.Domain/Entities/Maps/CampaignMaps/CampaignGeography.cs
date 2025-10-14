using AscendantEmpire.Domain.Enums.Maps;

using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace AscendantEmpire.Domain.Entities.Maps.CampaignMaps;

[Table("CampaignGeography", Schema = "map")]
public class CampaignGeography
{
    /// <summary>
    /// Unique identifier for this Campaign Geography
    /// </summary>
    [Key, DatabaseGenerated(DatabaseGeneratedOption.None)]
    public CampaignMapGeographyType CampaignMapGeographyTypeId { get; set; }

    /// <summary>
    /// User-friendly name of this Biome
    /// </summary>
    [MaxLength(50)]
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
    [MaxLength(50)]
    public required string? MapEditorIcon { get; set; }

    /// <summary>
    /// Whether this Geography can be painted on the Campaign Map or not.
    /// </summary>
    public required bool IsPaintableOnCampaignMap { get; set; }
}
