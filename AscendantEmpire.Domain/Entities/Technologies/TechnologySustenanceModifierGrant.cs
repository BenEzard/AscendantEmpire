using AscendantEmpire.Domain.Enums.Maps;

namespace AscendantEmpire.Domain.Entities.Technologies;

/// <summary>
/// One sustenance modifier a Technology grants when it is discovered, for one resource, in one biome or in all of them.
/// </summary>
/// <remarks>
/// <para>
/// Authored catalogue data, the same in every game. Discovering the Technology does not copy a figure from here onto a running total: a Player's
/// <c>PlayerSustenanceModifier</c> rows are derived from their discovered set crossed with this table, so re-pricing a grant and re-seeding corrects every game that
/// has already granted it. <c>IPlayerTechnologySustenanceModifierService</c> is the only writer of those rows.
/// </para>
/// <para>
/// <b><see cref="Resource"/> rightwards mirrors <c>PlayerSustenanceModifier</c>'s target and value columns exactly.</b> That is deliberate: with
/// <c>SourceType = Technology</c> and <c>SourceId = TechnologyId</c>, the player table's unique key is this table's unique key plus the Player, so a row here maps
/// one-to-one onto a row there and the reconciler is a projection rather than a transformation.
/// </para>
/// <para>
/// Unlike <see cref="TechnologyGrant"/>, which points at a different table per target type and so carries no foreign key, this points at exactly one thing and keeps
/// a real one. Modifiers cannot use <see cref="TechnologyGrant"/> at all: an unlock is a pointer plus a verb, while a modifier is parameters - a resource, a biome
/// and two values - with no row to point at.
/// </para>
/// <para>
/// A sustenance modifier can never give a biome a resource it does not have: <c>SustenanceValueCalculator</c> leaves a null base null whatever the modifiers say, so
/// the upsert rejects a grant naming a biome with no base for its resource rather than letting it be a permanent no-op. See
/// <c>docs/design/campaign-map/food-yield.md</c>.
/// </para>
/// </remarks>
public class TechnologySustenanceModifierGrant
{
    /// <summary>
    /// Surrogate key.
    /// </summary>
    /// <remarks>
    /// The natural key would include <see cref="BiomeTypeId"/>, which is nullable and so cannot be part of a primary key - the same trade
    /// <c>PlayerSustenanceModifier</c> makes, and the reason this table alone among the Technology's children does not use a composite natural key. Uniqueness is
    /// enforced by the <c>UX_TechnologySustenanceModifierGrant_Technology_Resource_Biome</c> index instead.
    /// </remarks>
    public int Id { get; set; }

    /// <summary>
    /// The Technology whose discovery grants this modifier.
    /// </summary>
    public int TechnologyId { get; set; }

    /// <summary>
    /// Which sustenance value is modified.
    /// </summary>
    public SustenanceResource Resource { get; set; }

    /// <summary>
    /// The biome whose value is modified, or <see langword="null"/> for every biome.
    /// </summary>
    public BiomeType? BiomeTypeId { get; set; }

    /// <summary>
    /// A flat amount added to the value, stored scaled by <c>ScaledAmount.Scale</c> (1,000). Negative for a penalty; 0 when this grant has no flat part.
    /// </summary>
    public long FlatValue { get; set; }

    /// <summary>
    /// A percentage of the base value added to the value, stored scaled by <c>ScaledPercent.Scale</c> (1,000 = 100%). Negative for a penalty; 0 when this grant has
    /// no percentage part.
    /// </summary>
    public long PercentValue { get; set; }

    public Technology Technology { get; set; } = null!;
}
