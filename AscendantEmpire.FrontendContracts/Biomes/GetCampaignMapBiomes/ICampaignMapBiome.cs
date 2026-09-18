using AscendantEmpire.Domain.Enums.Maps;

namespace AscendantEmpire.FrontendContracts.Biomes.GetCampaignMapBiomes;

public interface ICampaignMapBiome
{
    #region General Properties
    public BiomeType BiomeType { get; }

    /// <summary>
    /// User-friendly name of this Biome
    /// </summary>
    public string Name { get; }

    /// <summary>
    /// The movement cost to traverse a Tile of this Biome
    /// </summary>
    public decimal MovementCost { get; }

    /// <summary>
    /// Does this Biome block Sight? (prevent a Unit/Structure from seeing through it)
    /// </summary>
    public bool DoesBlockSight { get; }

    /// <summary>
    /// Is this Biome a water tile? (i.e. Ocean, Lake, River, etc.)
    /// </summary>
    public bool IsWater { get; }

    /// <summary>
    /// Is this Biome traversable? (i.e. can a Unit move through it)
    /// </summary>
    public bool IsTraversable { get; }

    /// <summary>
    /// The share of each sustenance base value that regrows at the end of every Round, as a fraction (0.25 is 25%), or <see langword="null"/> when the Biome has no sustenance.
    /// </summary>
    public decimal? RegenerationPercent { get; }

    #endregion

    #region Map Editor Properties
    /// <summary>
    /// Can this Biome be painted on a Campaign Map (by a map designer)?
    /// </summary>
    public bool IsPaintableOnCampaignMap { get; }

    /// <summary>
    /// HTML color string which this Biome will be shown as in the Map Editor
    /// </summary>
    public string MapEditorHtmlColor { get; }

    /// <summary>
    /// Can this Biome be elevated? (i.e. can it be Hilly or Mountainous)
    /// </summary>
    public bool CanBeElevated { get; }
    #endregion

    #region Passive Regenerating Resources
    /// <summary>
    /// Base value of the Forage resource in this Biome
    /// </summary>
    /// <remarks>If value is null then that means it can NEVER be increased/set</remarks>
    public int? BaseForageValue { get; }

    /// <summary>
    /// Base value of the Hunt resource in this Biome
    /// </summary>
    /// <remarks>If value is null then that means it can NEVER be increased/set</remarks>
    public int? BaseHuntValue { get; }

    /// <summary>
    /// Base value of the Fish resource in this Biome
    /// </summary>
    /// <remarks>If value is null then that means it can NEVER be increased/set</remarks>
    public int? BaseFishValue { get; }

    /// <summary>
    /// Base value of the Fodder resource in this Biome
    /// </summary>
    /// <remarks>If value is null then that means it can NEVER be increased/set</remarks>
    public int? BaseFodderValue { get; }
    #endregion

    #region Passive Material Values
    /// <summary>
    /// Base value of the Bone resource in this Biome
    /// </summary>
    /// <remarks>If value is null then that means it can NEVER be increased/set</remarks>
    public int? BaseBoneValue { get; }

    /// <summary>
    /// Base value of the Wood resource in this Biome
    /// </summary>
    /// <remarks>If value is null then that means it can NEVER be increased/set</remarks>
    public int? BaseWoodValue { get; }

    /// <summary>
    /// Base value of the Stone resource in this Biome
    /// </summary>
    /// <remarks>If value is null then that means it can NEVER be increased/set</remarks>
    public int? BaseStoneValue { get; }

    /// <summary>
    /// Base value of the Clay resource in this Biome
    /// </summary>
    /// <remarks>If value is null then that means it can NEVER be increased/set</remarks>
    public int? BaseSpecialValue { get; }
    #endregion
}
