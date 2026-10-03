using AscendantEmpire.Domain.Enums.Technologies;

namespace AscendantEmpire.FrontendContracts.Technologies.GetTechnologies;

/// <summary>
/// One thing a technology unlocks or retires when it is discovered.
/// </summary>
/// <remarks>
/// The code and display name are null when the target no longer exists - a grant carries no foreign key, so it can outlive what it points at.
/// </remarks>
public interface ICatalogueGrant
{
    TechnologyGrantTargetType TargetType { get; }

    int TargetId { get; }

    string? TargetCode { get; }

    string? TargetDisplayName { get; }

    TechnologyGrantEffect GrantEffect { get; }
}
