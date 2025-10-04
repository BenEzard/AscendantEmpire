using AscendantEmpire.Domain.Enums.Maps;

using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace AscendantEmpire.Domain.Entities.Maps.CampaignMaps;

[Table("CampaignBiomes", Schema = "map")]
public class CampaignBiome
{
    #region General Properties
    /// <summary>
    /// Unique identifier for this Biome
    /// </summary>
    [Key, DatabaseGenerated(DatabaseGeneratedOption.None)]
    public BiomeType BiomeTypeId { get; set; }

    /// <summary>
    /// User-friendly name of this Biome
    /// </summary>
    [MaxLength(50)]
    public string Name { get; set; } = null!;

    /// <summary>
    /// The movement cost to traverse a Tile of this Biome
    /// </summary>
    public decimal MovementCost { get; set; }

    /// <summary>
    /// Does this Biome block Sight? (prevent a Unit/Structure from seeing through, or out of, it)
    /// </summary>
    public bool DoesBlockSight { get; set; }

    /// <summary>
    /// Is this Biome a water tile? (i.e. Ocean, Lake, River, etc.)
    /// </summary>
    public bool IsWater { get; set; }

    /// <summary>
    /// Is this Biome traversable? (i.e. can a Unit move through it)
    /// </summary>
    public bool IsTraversable { get; set; }

    /// <summary>
    /// How quickly does this Biome regenerate resources?
    /// </summary>
    /// <remarks>Only applicable for Biomes that can be painted on a Campaign Map</remarks>
    public decimal? RegenerationRate { get; set; } = 1.1m;
    #endregion

    #region Map Editor Properties
    /// <summary>
    /// Can this Biome be painted on a Campaign Map (by a map designer)?
    /// </summary>
    public bool IsPaintableOnCampaignMap { get; set; }

    /// <summary>
    /// HTML color string which this Biome will be shown as in the Map Editor
    /// </summary>
    public string MapEditorHtmlColor { get; set; } = null!;

    /// <summary>
    /// Can this Biome be elevated? (i.e. can it be Hilly or Mountainous)
    /// </summary>
    public bool CanBeElevated { get; set; }
    #endregion

    #region Passive Regenerating Resources
    /// <summary>
    /// Base value of the Forage resource in this Biome
    /// </summary>
    public int? BaseForageValue { get; set; }

    /// <summary>
    /// Base value of the Hunt resource in this Biome
    /// </summary>
    public int? BaseHuntValue { get; set; }

    /// <summary>
    /// Base value of the Fish resource in this Biome
    /// </summary>
    public int? BaseFishValue { get; set; }

    /// <summary>
    /// Base value of the Fodder resource in this Biome
    /// </summary>
    public int? BaseFodderValue { get; set; }
    #endregion

    #region Passive Material Values
    /// <summary>
    /// Base value of the Bone resource in this Biome
    /// </summary>
    public int? BaseBoneValue { get; set; }

    /// <summary>
    /// Base value of the Wood resource in this Biome
    /// </summary>
    public int? BaseWoodValue { get; set; }

    /// <summary>
    /// Base value of the Stone resource in this Biome
    /// </summary>
    public int? BaseStoneValue { get; set; }

    /// <summary>
    /// Base value of the Special resource in this Biome
    /// </summary>
    public int? BaseSpecialValue { get; set; }
    #endregion
}
