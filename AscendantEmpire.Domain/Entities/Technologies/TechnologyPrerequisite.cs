using AscendantEmpire.Domain.Enums.Technologies;

namespace AscendantEmpire.Domain.Entities.Technologies;

/// <summary>
/// A directed "this Technology requires that one" edge in the global catalogue.
/// </summary>
/// <remarks>
/// <para>
/// Authored catalogue data, not per-Player. The relationship is many-to-many so the tree can branch and merge, in the same style as
/// <c>ComponentSupersession</c>. Composite primary key on (TechnologyId, PrerequisiteTechnologyId).
/// </para>
/// <para>
/// A Technology becomes available to a Player once <b>all</b> of its <see cref="TechnologyPrerequisiteKind.Mandatory"/> edges are discovered <b>and</b>, if it
/// has any <see cref="TechnologyPrerequisiteKind.Optional"/> edges at all, at least one of those is too.
/// </para>
/// <para>
/// <b>Cycles are fatal and the database cannot prevent them.</b> A cycle makes every Technology in it permanently unreachable, and the availability recompute
/// never settles. The upsert handler has to reject them; a self-edge is caught by a check constraint, but nothing longer is.
/// </para>
/// </remarks>
public class TechnologyPrerequisite
{
    /// <summary>
    /// The Technology that is gated.
    /// </summary>
    public int TechnologyId { get; set; }

    /// <summary>
    /// The Technology that gates it, which must be discovered first.
    /// </summary>
    public int PrerequisiteTechnologyId { get; set; }

    /// <summary>
    /// Whether this edge must be satisfied, or is one of a set of alternatives.
    /// </summary>
    public TechnologyPrerequisiteKind PrerequisiteKind { get; set; }

    public Technology Technology { get; set; } = null!;

    public Technology PrerequisiteTechnology { get; set; } = null!;
}
