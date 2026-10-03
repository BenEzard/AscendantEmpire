using AscendantEmpire.Domain.Enums.Technologies;

namespace AscendantEmpire.BackendContracts.Technologies;

/// <summary>
/// One Technology that must be discovered before another.
/// </summary>
/// <remarks>
/// The prerequisite is named by code, and may be a Technology defined elsewhere in the same request or one already in the database.
/// </remarks>
public record UpsertTechnologyPrerequisiteDto
{
    /// <summary>
    /// The code of the Technology that must come first.
    /// </summary>
    public required string PrerequisiteTechnologyCode { get; init; }

    /// <summary>
    /// Whether this edge must be satisfied, or is one of a set of alternatives.
    /// </summary>
    /// <remarks>Defaults to mandatory, which is the common case - optional edges are the deliberate exception.</remarks>
    public TechnologyPrerequisiteKind PrerequisiteKind { get; init; } = TechnologyPrerequisiteKind.Mandatory;
}
