using AscendantEmpire.Domain.Enums.Maps;
using AscendantEmpire.Domain.Enums.MapResources;
using AscendantEmpire.Domain.Enums.Modifiers;

namespace AscendantEmpire.Domain.Entities.Games;

/// <summary>
/// One modifier to one of a Player's values for a MapResourceType, granted by one source, for one Biome and Geography or for all of them.
/// </summary>
/// <remarks>
/// <para>
/// The same shape as <see cref="PlayerSustenanceModifier"/>, with a Geography added so a modifier can target, say, Hilly Forest. A Player's value is never stored: it is
/// derived from the base value and the modifiers the Player currently holds, <c>Base + Base × ΣPercentValue + ΣFlatValue</c>, so losing a source is just deleting its rows.
/// </para>
/// <para>
/// The habitat values (<see cref="MapResourceModifierTarget.MaximumQuantity"/>, <see cref="MapResourceModifierTarget.GrowthPercentage"/> and
/// <see cref="MapResourceModifierTarget.SpreadLikelihood"/>) apply only on tiles the Player has claimed, representing good management; unclaimed tiles use the habitat's own
/// values, whoever is on them. See "Player modifiers" in <c>docs/design/campaign-map/map-resources.md</c>.
/// </para>
/// <para>
/// No navigation properties, the same as <see cref="PlayerSustenanceModifier"/>: the rows are summed, never walked as an entity graph.
/// </para>
/// </remarks>
public class PlayerMapResourceModifier
{
    /// <summary>
    /// Surrogate key.
    /// </summary>
    /// <remarks>
    /// The natural key includes the nullable <see cref="BiomeTypeId"/> and <see cref="CampaignMapGeographyTypeId"/>, which cannot be part of a primary key. Uniqueness is
    /// enforced by the <c>UX_PlayerMapResourceModifier_Player_Source_Type_Target_Biome_Geography</c> index instead.
    /// </remarks>
    public int Id { get; set; }

    /// <summary>
    /// The Game the Player belongs to, carried so the game delete can purge these rows directly.
    /// </summary>
    public int GameId { get; set; }

    /// <summary>
    /// The Player whose value is modified.
    /// </summary>
    public int PlayerId { get; set; }

    /// <summary>
    /// What kind of thing granted this modifier.
    /// </summary>
    public ModifierSourceType SourceType { get; set; }

    /// <summary>
    /// The particular Technology, Specialist or Department that granted this modifier, according to <see cref="SourceType"/>.
    /// </summary>
    /// <remarks>
    /// Not a foreign key: it points at a different table for each source type, and none of those tables exist yet.
    /// </remarks>
    public int SourceId { get; set; }

    /// <summary>
    /// The type whose value is modified (a <c>map.MapResourceType</c>).
    /// </summary>
    public int MapResourceTypeId { get; set; }

    /// <summary>
    /// Which value is modified.
    /// </summary>
    public MapResourceModifierTarget Target { get; set; }

    /// <summary>
    /// The Biome whose value is modified, or <see langword="null"/> for every Biome.
    /// </summary>
    public BiomeType? BiomeTypeId { get; set; }

    /// <summary>
    /// The Geography whose value is modified, or <see langword="null"/> for every Geography.
    /// </summary>
    public CampaignMapGeographyType? CampaignMapGeographyTypeId { get; set; }

    /// <summary>
    /// A flat amount added to the value. Negative for a penalty; 0 when this modifier has no flat part.
    /// </summary>
    /// <remarks>
    /// For <see cref="MapResourceModifierTarget.MaximumQuantity"/> an amount, stored scaled by <c>ScaledAmount.Scale</c> (1,000). For every other target the value is a
    /// fraction, so this is percentage points, stored scaled by <c>ScaledPercent.Scale</c> (1,000 = 100 points).
    /// </remarks>
    public long FlatValue { get; set; }

    /// <summary>
    /// A percentage of the base value added to the value, stored scaled by <c>ScaledPercent.Scale</c> (1,000 = 100%). Negative for a penalty; 0 when this modifier has no
    /// percentage part.
    /// </summary>
    public long PercentValue { get; set; }
}
