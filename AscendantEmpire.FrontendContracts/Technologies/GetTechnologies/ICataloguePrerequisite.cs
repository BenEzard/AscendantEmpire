using AscendantEmpire.Domain.Enums.Technologies;

namespace AscendantEmpire.FrontendContracts.Technologies.GetTechnologies;

/// <summary>
/// One technology that must be discovered before another.
/// </summary>
public interface ICataloguePrerequisite
{
    int PrerequisiteTechnologyId { get; }

    string PrerequisiteTechnologyCode { get; }

    string PrerequisiteTechnologyDisplayName { get; }

    TechnologyPrerequisiteKind PrerequisiteKind { get; }
}
