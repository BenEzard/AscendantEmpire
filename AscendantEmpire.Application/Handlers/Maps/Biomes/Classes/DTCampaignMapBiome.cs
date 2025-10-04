using AscendantEmpire.Domain.Enums.Maps;
using AscendantEmpire.FrontendContracts.Biomes.GetCampaignMapBiomes;

namespace AscendantEmpire.Application.Handlers.Maps.Biomes.Classes;

public class DTCampaignMapBiome : ICampaignMapBiome
{
    #region General Properties
    public BiomeType BiomeType { get; set; }

    /// <summary>
    /// User-friendly name of this Biome
    /// </summary>
    public required string Name { get; set; }

    /// <summary>
    /// The movement cost to traverse a Tile of this Biome
    /// </summary>
    public decimal MovementCost { get; set; }

    /// <summary>
    /// Does this Biome block Sight? (prevent a Unit/Structure from seeing through it)
    /// </summary>
    public bool DoesBlockSight { get; set; }

    /// <summary>
    /// Is this Biome a water tile? (i.e. Ocean, Lake, River, etc.)
    /// </summary>
    public bool IsWater { get; set; }

    /// <summary>
    /// Is this Biome traversable? (i.e. can a Unit move through it)
    public bool IsTraversable { get; set; }

    /// <summary>
    /// The rate at which this Biome regenerates resources
    /// <summary>
    public decimal? RegenerationRate { get; set; }
    #endregion

    #region Map Editor Properties
    /// <summary>
    /// Can this Biome be painted on a Campaign Map (by a map designer)?
    /// </summary>
    public bool IsPaintableOnCampaignMap { get; set; }

    /// <summary>
    /// HTML color string which this Biome will be shown as in the Map Editor
    /// </summary>
    public required string MapEditorHtmlColor { get; set; }

    /// <summary>
    /// Can this Biome be elevated? (i.e. can it be Hilly or Mountainous)
    /// </summary>
    public bool CanBeElevated { get; set; }
    #endregion
    
    #region Passive Regenerating Resources
    /// <summary>
    /// The base value of the Forage resource in this Biome
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

