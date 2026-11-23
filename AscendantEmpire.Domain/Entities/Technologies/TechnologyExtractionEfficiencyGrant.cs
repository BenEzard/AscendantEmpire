using AscendantEmpire.Domain.Entities.Assets;

namespace AscendantEmpire.Domain.Entities.Technologies;

/// <summary>
/// One extraction efficiency modifier a Technology grants when it is discovered, for one Asset.
/// </summary>
/// <remarks>
/// <para>
/// Authored catalogue data, the same in every game. A Player's <c>PlayerExtractionEfficiencyModifier</c> rows are derived from their discovered set crossed with
/// this table, so re-pricing a grant and re-seeding corrects every game that has already granted it. With <c>SourceType = Technology</c> and
/// <c>SourceId = TechnologyId</c> a row here maps one-to-one onto a row there, so the reconciler is a projection rather than a transformation.
/// </para>
/// <para>
/// Efficiency is how much of what the land holds a gatherer gets out, not what the land holds - that is the yield modifiers' job. Both values are
/// <c>ScaledPercent</c>-scaled (1,000 = 100): <see cref="FlatValue"/> is percentage points added to the efficiency, and <see cref="PercentValue"/> a percentage of
/// the starting efficiency the extraction path supplies. The catalogue carries no starting efficiency, because that belongs to the path that reads it.
/// </para>
/// </remarks>
public class TechnologyExtractionEfficiencyGrant
{
    /// <summary>
    /// The Technology whose discovery grants this modifier.
    /// </summary>
    public int TechnologyId { get; set; }

    /// <summary>
    /// The naturally occurring Asset whose extraction efficiency is modified.
    /// </summary>
    public int AssetId { get; set; }

    /// <summary>
    /// Percentage points added to the efficiency, stored scaled by <c>ScaledPercent.Scale</c>. Negative for a penalty; 0 when this grant has no flat part.
    /// </summary>
    public long FlatValue { get; set; }

    /// <summary>
    /// A percentage of the starting efficiency added to the efficiency, stored scaled by <c>ScaledPercent.Scale</c>. Negative for a penalty; 0 when this grant has no
    /// percentage part.
    /// </summary>
    public long PercentValue { get; set; }

    public Technology Technology { get; set; } = null!;

    public Asset Asset { get; set; } = null!;
}
