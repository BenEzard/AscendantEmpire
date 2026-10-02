using AscendantEmpire.Domain.Enums.Maps;

using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace AscendantEmpire.Domain.Entities.Maps.CampaignMaps;

public class CampaignBiome
{
    public const int NAME_MAX_LENGTH = 50;

    #region General Properties
    /// <summary>
    /// Unique identifier for this Biome
    /// </summary>
    [Key, DatabaseGenerated(DatabaseGeneratedOption.None)]
    public BiomeType BiomeTypeId { get; set; }

    /// <summary>
    /// User-friendly name of this Biome
    /// </summary>
    [MaxLength(NAME_MAX_LENGTH)]
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
    /// The share of each sustenance base value that regrows at the end of every Round, as a fraction (0.25 is 25%).
    /// </summary>
    /// <remarks>
    /// <para>
    /// The same share applies to all of the Biome's sustenance (Forage, Hunt, Fish, Fodder): at the end of each Round a tile gets back this fraction of its <c>Base</c>, fed on
    /// or not, capped at <c>Base</c>. See "Replenishment" in <c>docs/design/campaign-map/replenishment.md</c>.
    /// </para>
    /// <para>
    /// Required, and more than 0 and at most 1, when the Biome has any sustenance base value; <see langword="null"/> when it has none, since there is nothing to regrow.
    /// Enforced by the biome validator. Stored to three decimal places, so the finest step is 0.1%.
    /// </para>
    /// </remarks>
    public decimal? RegenerationPercent { get; set; }
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

    // NOTE: The Base*Value asset fields below are stored SCALED by ScaledAmount.Scale (1,000).
    // The UI/API work in whole numbers (e.g. a designer enters 50); the handlers convert to/from the
    // scaled raw value (e.g. 50,000) so downstream fixed-point bonus math keeps sub-unit precision.
    // That scaling is why these are long? (bigint) rather than int? -- there must be headroom for the
    // multiplication.

    #region Passive Regenerating Assets
    /// <summary>
    /// Base value of the Forage asset in this Biome, stored scaled by ScaledAmount.Scale (1,000)
    /// </summary>
    public long? BaseForageValue { get; set; }

    /// <summary>
    /// Base value of the Hunt asset in this Biome, stored scaled by ScaledAmount.Scale (1,000)
    /// </summary>
    public long? BaseHuntValue { get; set; }

    /// <summary>
    /// Base value of the Fish asset in this Biome, stored scaled by ScaledAmount.Scale (1,000)
    /// </summary>
    public long? BaseFishValue { get; set; }

    /// <summary>
    /// Base value of the Fodder asset in this Biome, stored scaled by ScaledAmount.Scale (1,000)
    /// </summary>
    public long? BaseFodderValue { get; set; }
    #endregion

    #region Passive Material Values
    /// <summary>
    /// Base value of the Bone asset in this Biome, stored scaled by ScaledAmount.Scale (1,000)
    /// </summary>
    public long? BaseBoneValue { get; set; }

    /// <summary>
    /// Base value of the Wood asset in this Biome, stored scaled by ScaledAmount.Scale (1,000)
    /// </summary>
    public long? BaseWoodValue { get; set; }

    /// <summary>
    /// Base value of the Stone asset in this Biome, stored scaled by ScaledAmount.Scale (1,000)
    /// </summary>
    public long? BaseStoneValue { get; set; }
    #endregion
}
