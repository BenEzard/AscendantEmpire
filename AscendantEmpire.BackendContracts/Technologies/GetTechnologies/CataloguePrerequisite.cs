using AscendantEmpire.Domain.Enums.Technologies;

namespace AscendantEmpire.BackendContracts.Technologies.GetTechnologies;

/// <summary>
/// One technology that must be discovered before another.
/// </summary>
/// <remarks>
/// All mandatory edges must be met, and where a technology has optional edges at least one of those must be met too.
/// </remarks>
public record CataloguePrerequisite
{
    public required int PrerequisiteTechnologyId { get; init; }

    public required string PrerequisiteTechnologyCode { get; init; }

    public required string PrerequisiteTechnologyDisplayName { get; init; }

    public required TechnologyPrerequisiteKind PrerequisiteKind { get; init; }
}
