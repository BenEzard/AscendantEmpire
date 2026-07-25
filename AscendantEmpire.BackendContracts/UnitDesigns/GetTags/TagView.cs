using AscendantEmpire.Domain.Enums.Maps;
using AscendantEmpire.Domain.Enums.UnitDesigns;

namespace AscendantEmpire.BackendContracts.UnitDesigns.GetTags;

/// <summary>
/// A single tag in the global catalog as seen by the dev Unit Designer tool.
/// </summary>
/// <remarks>
/// Like <c>CatalogTag</c> but also carries <see cref="TagType"/>, which the tag-management UI needs to
/// display and edit (the read-only god-view catalog omits it).
/// </remarks>
public record TagView
{
    public required int Id { get; init; }

    public required string Code { get; init; }

    public required string DisplayName { get; init; }

    public string? Description { get; init; }

    public required TagType TagType { get; init; }

    /// <summary>
    /// The movement domain for a MilitaryOrganisationalGroup tag; null for other tag types (or an
    /// organisation tag with no domain assigned yet).
    /// </summary>
    public DomainType? MovementDomain { get; init; }
}
