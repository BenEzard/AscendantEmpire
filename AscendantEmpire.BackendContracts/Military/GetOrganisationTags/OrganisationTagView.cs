using AscendantEmpire.Domain.Enums.Maps;

namespace AscendantEmpire.BackendContracts.Military.GetOrganisationTags;

/// <summary>
/// A MilitaryOrganisationalGroup tag offered in the Organisation page's searchable combo box.
/// </summary>
public record OrganisationTagView
{
    public required int Id { get; init; }

    public required string Code { get; init; }

    public required string DisplayName { get; init; }

    /// <summary>The domain this organisation group belongs to; null if not assigned one yet.</summary>
    public DomainType? MovementDomain { get; init; }
}
