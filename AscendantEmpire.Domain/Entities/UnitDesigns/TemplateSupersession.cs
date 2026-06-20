namespace AscendantEmpire.Domain.Entities.UnitDesigns;

/// <summary>
/// A directed "successor supersedes predecessor" link between global unit templates.
/// </summary>
/// <remarks>
/// <para>Mirrors <see cref="ComponentSupersession"/> at the whole-design level: it records that
/// <see cref="Successor"/> is an upgraded design replacing <see cref="Predecessor"/>. Many-to-many so
/// upgrade paths can branch and merge. Composite primary key on
/// (PredecessorTemplateId, SuccessorTemplateId).</para>
/// <para>Authored against global/common templates (those with a <c>Code</c>); per-player obsolescence
/// stays on <see cref="PlayerUnlockedTemplate.ObsoleteOnTurn"/>.</para>
/// </remarks>
public class TemplateSupersession
{
    /// <summary>
    /// The older template being superseded.
    /// </summary>
    public int PredecessorTemplateId { get; set; }

    /// <summary>
    /// The newer template that supersedes the predecessor.
    /// </summary>
    public int SuccessorTemplateId { get; set; }

    /// <summary>
    /// Whether this is the recommended/default upgrade among a predecessor's successors. At most one
    /// successor per predecessor should be flagged.
    /// </summary>
    public bool IsRecommended { get; set; }

    public UnitTemplate Predecessor { get; set; } = null!;
    public UnitTemplate Successor { get; set; } = null!;
}
