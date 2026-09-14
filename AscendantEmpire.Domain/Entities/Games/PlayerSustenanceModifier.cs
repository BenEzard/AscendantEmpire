using AscendantEmpire.Domain.Enums.Maps;
using AscendantEmpire.Domain.Enums.Modifiers;
using AscendantEmpire.Domain.Enums.Resources;

namespace AscendantEmpire.Domain.Entities.Games;

/// <summary>
/// One modifier to one of a Player's sustenance values (Forage, Hunt, Fish or Fodder), granted by one source, for one biome or for all of them.
/// </summary>
/// <remarks>
/// <para>
/// A Player's effective value is never stored. It is derived from the biome's base value and the modifiers the Player currently holds:
/// <c>Base + Base × ΣPercentValue + ΣFlatValue</c>. Sources can be lost again (a Specialist dies, a Department is dissolved), and deriving the value means losing one is
/// just deleting its rows by (<see cref="SourceType"/>, <see cref="SourceId"/>) - there is no running total to unwind. <c>IPlayerSustenanceValueReader</c> is the only
/// place that formula lives.
/// </para>
/// <para>
/// <list type="bullet">
///   <item><description>A <c>null</c> base value means the resource does not exist in that biome, and no modifier ever gives it one.</description></item>
///   <item><description>Percentages add together rather than compound, and apply to the base only - never to the flat modifiers.</description></item>
///   <item><description>The summed percentage is floored at −100%, and the effective value at 0.</description></item>
/// </list>
/// </para>
/// <para>
/// Only empire-wide modifiers exist so far. A Settlement's modifiers will apply to its territory, and wait on Settlements being modelled - TODO: add a nullable
/// <c>SettlementId</c> scope then. A Force's gathering bonuses (a forager attached to a Command) are not modifiers of this kind at all: they reduce what the Force draws
/// from a tile rather than raising the tile's maximum. See the Biome section of <c>docs/design/campaign-map.md</c>.
/// </para>
/// <para>
/// No navigation properties, the same as <see cref="PlayerBattleMap"/>: the rows are summed, never walked as an entity graph.
/// </para>
/// </remarks>
public class PlayerSustenanceModifier
{
    /// <summary>
    /// Surrogate key.
    /// </summary>
    /// <remarks>
    /// The natural key would include <see cref="BiomeTypeId"/>, which is nullable and so cannot be part of a primary key. Uniqueness is enforced by the
    /// <c>UX_PlayerSustenanceModifier_Player_Source_Resource_Biome</c> index instead.
    /// </remarks>
    public int Id { get; set; }

    /// <summary>
    /// The Game the Player belongs to.
    /// </summary>
    /// <remarks>
    /// Implied by <see cref="PlayerId"/>, but carried so the game delete can purge these rows directly rather than through the Player.
    /// </remarks>
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
    /// Which sustenance value is modified.
    /// </summary>
    public SustenanceResource Resource { get; set; }

    /// <summary>
    /// The biome whose value is modified, or <see langword="null"/> for every biome.
    /// </summary>
    public BiomeType? BiomeTypeId { get; set; }

    /// <summary>
    /// A flat amount added to the value, stored scaled by <c>ScaledAmount.Scale</c> (1,000). Negative for a penalty; 0 when this modifier has no flat part.
    /// </summary>
    public long FlatValue { get; set; }

    /// <summary>
    /// A percentage of the base value added to the value, stored scaled by <c>ScaledPercent.Scale</c> (1,000 = 100%). Negative for a penalty; 0 when this modifier has no
    /// percentage part.
    /// </summary>
    public long PercentValue { get; set; }
}
