using AscendantEmpire.Domain.Enums.Maps;
using AscendantEmpire.Domain.Enums.UnitDesigns;

namespace AscendantEmpire.BackendContracts.UnitDesigns.GetTags;

/// <summary>
/// A single tag in the global catalogue as seen by the dev Unit Designer tool.
/// </summary>
/// <remarks>
/// Like <c>CatalogueTag</c> but also carries <see cref="TagType"/>, which the tag-management UI needs to
/// display and edit (the read-only god-view catalogue omits it).
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

    /// <summary>
    /// The leadership model for a MilitaryOrganisationalGroup tag; null for other tag types.
    /// </summary>
    public LeadershipModel? LeadershipModel { get; init; }

    /// <summary>
    /// The indivisible group size (quantum) for an Unled tag; null otherwise.
    /// </summary>
    public int? UnledBaseSize { get; init; }

    /// <summary>
    /// The size an unled unit takes when created without an explicit choice; null unless the tag is unled.
    /// </summary>
    public int? UnledDefaultSize { get; init; }

    /// <summary>
    /// The largest a single unled unit may be; null unless the tag is unled.
    /// </summary>
    public int? UnledMaxSize { get; init; }

    /// <summary>
    /// Whether the tag is system-created (seeded/built-in) rather than user-authored.
    /// </summary>
    public required bool SystemCreated { get; init; }
}
