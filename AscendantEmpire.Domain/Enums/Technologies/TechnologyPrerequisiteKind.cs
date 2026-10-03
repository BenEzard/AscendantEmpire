namespace AscendantEmpire.Domain.Enums.Technologies;

/// <summary>
/// Whether a prerequisite edge must be satisfied, or merely contributes to a choice.
/// </summary>
/// <remarks>
/// A Technology becomes available once <b>all</b> of its mandatory prerequisites are discovered <b>and</b>, if it has any optional ones at all, at least one of
/// those is discovered too. A Technology with no prerequisite rows is available from the start of the game.
/// </remarks>
public enum TechnologyPrerequisiteKind
{
    /// <summary>
    /// Must be discovered. Every mandatory prerequisite has to be met.
    /// </summary>
    Mandatory = 1,

    /// <summary>
    /// One of a set. Where a Technology has optional prerequisites, at least one of them must be discovered.
    /// </summary>
    Optional = 2
}
