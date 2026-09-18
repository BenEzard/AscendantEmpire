using AscendantEmpire.Domain.Enums.Maps;

namespace AscendantEmpire.BackendContracts.Biomes;

public record CampaignMapBiome
{
    #region General Properties
    public required BiomeType BiomeType { get; init; }

    /// <summary>
    /// User-friendly name of this Biome
    /// </summary>
    public required string Name { get; init; }

    /// <summary>
    /// The movement cost to traverse a Tile of this Biome
    /// </summary>
    public required decimal MovementCost { get; init; }

    /// <summary>
    /// Does this Biome block Sight? (prevent a Unit/Structure from seeing through it)
    /// </summary>
    public required bool DoesBlockSight { get; init; }

    /// <summary>
    /// Is this Biome a water tile? (i.e. Ocean, Lake, River, etc.)
    /// </summary>
    public required bool IsWater { get; init; }

    /// <summary>
    /// Is this Biome traversable? (i.e. can a Unit move through it)
    /// </summary>
    public required bool IsTraversable { get; init; }

    /// <summary>
    /// The share of each sustenance base value that regrows at the end of every Round, as a fraction (0.25 is 25%), or <see langword="null"/> when the Biome has no sustenance.
    /// </summary>
    public required decimal? RegenerationPercent { get; init; }
    #endregion

    #region Map Editor Properties
    /// <summary>
    /// Can this Biome be painted on a Campaign Map (by a map designer)?
    /// </summary>
    public required bool IsPaintableOnCampaignMap { get; init; }

    /// <summary>
    /// HTML color string which this Biome will be shown as in the Map Editor
    /// </summary>
    public required string MapEditorHtmlColor { get; init; }

    /// <summary>
    /// Can this Biome be elevated? (i.e. can it be Hilly or Mountainous)
    /// </summary>
    public required bool CanBeElevated { get; init; }
    #endregion

    #region Passive Regenerating Resources
    /// <summary>
    /// Base value of the Forage resource in this Biome
    /// </summary>
    /// <remarks>If value is null it can NEVER be increased/set</remarks>
    public int? BaseForageValue { get; init; }

    /// <summary>
    /// Base value of the Hunt resource in this Biome
    /// </summary>
    /// <remarks>If value is null it can NEVER be increased/set</remarks>
    public int? BaseHuntValue { get; init; }

    /// <summary>
    /// Base value of the Fish resource in this Biome
    /// </summary>
    /// <remarks>If value is null it can NEVER be increased/set</remarks>
    public int? BaseFishValue { get; init; }

    /// <summary>
    /// Base value of the Fodder resource in this Biome
    /// </summary>
    /// <remarks>If value is null it can NEVER be increased/set</remarks>
    public int? BaseFodderValue { get; init; }
    #endregion

    #region Passive Material Values
    /// <summary>
    /// Base value of the Bone resource in this Biome
    /// </summary>
    /// <remarks>If value is null it can NEVER be increased/set</remarks>
    public int? BaseBoneValue { get; init; }

    /// <summary>
    /// Base value of the Wood resource in this Biome
    /// </summary>
    /// <remarks>If value is null it can NEVER be increased/set</remarks>
    public int? BaseWoodValue { get; init; }

    /// <summary>
    /// Base value of the Stone resource in this Biome
    /// </summary>
    /// <remarks>If value is null it can NEVER be increased/set</remarks>
    public int? BaseStoneValue { get; init; }

    /// <summary>
    /// Base value of the Special resource in this Biome
    /// </summary>
    /// <remarks>If value is null it can NEVER be increased/set</remarks>
    public int? BaseSpecialValue { get; init; }
    #endregion
}
