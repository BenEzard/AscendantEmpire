using AscendantEmpire.Domain.Enums.Modifiers;

namespace AscendantEmpire.Domain.Entities.Games;

/// <summary>
/// One modifier to a Player's extraction efficiency for one Asset, granted by one source.
/// </summary>
/// <remarks>
/// <para>
/// Efficiency is how much of what the land holds a Player's gatherers get out, applied to the take rather than to the tile, so it sits beside the yield modifiers
/// (<see cref="PlayerSustenanceModifier"/>, <see cref="PlayerMapResourceModifier"/>) rather than changing what they mean.
/// </para>
/// <para>
/// A Player's efficiency is never stored. It is derived as <c>Start + Start × ΣPercentValue + ΣFlatValue</c>, floored at 0, where the starting efficiency belongs
/// to the extraction path that reads it rather than to this row. <c>ExtractionEfficiencyCalculator</c> is the only place that formula lives. Losing a source is
/// just deleting its rows by (<see cref="SourceType"/>, <see cref="SourceId"/>).
/// </para>
/// <para>
/// No navigation properties, the same as <see cref="PlayerSustenanceModifier"/>: the rows are summed, never walked as an entity graph.
/// </para>
/// </remarks>
public class PlayerExtractionEfficiencyModifier
{
    /// <summary>
    /// Surrogate key, matching <see cref="PlayerSustenanceModifier"/>. Uniqueness is enforced by the <c>UX_PlayerExtractionEfficiencyModifier_Player_Source_Asset</c>
    /// index instead.
    /// </summary>
    public int Id { get; set; }

    /// <summary>
    /// The Game the Player belongs to, carried so the game delete can purge these rows directly.
    /// </summary>
    public int GameId { get; set; }

    /// <summary>
    /// The Player whose efficiency is modified.
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
    /// Not a foreign key: it points at a different table for each source type.
    /// </remarks>
    public int SourceId { get; set; }

    /// <summary>
    /// The naturally occurring Asset whose extraction efficiency is modified.
    /// </summary>
    public int AssetId { get; set; }

    /// <summary>
    /// Percentage points added to the efficiency, stored scaled by <c>ScaledPercent.Scale</c> (1,000 = 100 points). Negative for a penalty; 0 when this modifier has
    /// no flat part.
    /// </summary>
    public long FlatValue { get; set; }

    /// <summary>
    /// A percentage of the starting efficiency added to the efficiency, stored scaled by <c>ScaledPercent.Scale</c> (1,000 = 100%). Negative for a penalty; 0 when this
    /// modifier has no percentage part.
    /// </summary>
    public long PercentValue { get; set; }
}
