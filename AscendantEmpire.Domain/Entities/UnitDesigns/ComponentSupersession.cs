namespace AscendantEmpire.Domain.Entities.UnitDesigns;

/// <summary>
/// A directed "successor supersedes predecessor" link in the global component catalog.
/// </summary>
/// <remarks>
/// <para>Authored catalog data (not per-player): it records that <see cref="Successor"/> is an upgrade
/// of <see cref="Predecessor"/>. The relationship is many-to-many so upgrade paths can branch (one
/// predecessor superseded by several options) and merge (several predecessors collapsing into one
/// successor). Composite primary key on (PredecessorComponentId, SuccessorComponentId).</para>
/// <para>This link only describes the path; per-player obsolescence stays on
/// <see cref="PlayerUnlockedComponent.ObsoleteOnTurn"/>.</para>
/// </remarks>
public class ComponentSupersession
{
    /// <summary>
    /// The older component being superseded.
    /// </summary>
    public int PredecessorComponentId { get; set; }

    /// <summary>
    /// The newer component that supersedes the predecessor.
    /// </summary>
    public int SuccessorComponentId { get; set; }

    /// <summary>
    /// Whether this is the recommended/default upgrade among a predecessor's successors. At most one
    /// successor per predecessor should be flagged.
    /// </summary>
    public bool IsRecommended { get; set; }

    public UnitComponent Predecessor { get; set; } = null!;
    public UnitComponent Successor { get; set; } = null!;
}
