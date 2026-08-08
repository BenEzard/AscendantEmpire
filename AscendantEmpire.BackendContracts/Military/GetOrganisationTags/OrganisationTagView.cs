using AscendantEmpire.Domain.Enums.Maps;
using AscendantEmpire.Domain.Enums.UnitDesigns;

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

    /// <summary>
    /// How formations under this tag are led; null when unspecified (treated as commissioned). Unled tags
    /// have no tier structure — the page shows their authored sizing read-only instead of the tier editor.
    /// </summary>
    public LeadershipModel? LeadershipModel { get; init; }

    /// <summary>The indivisible group size (quantum) for an Unled tag; null otherwise.</summary>
    public int? UnledBaseSize { get; init; }

    /// <summary>The size an unled unit takes when created without an explicit choice; null unless unled.</summary>
    public int? UnledDefaultSize { get; init; }

    /// <summary>The largest a single unled unit may be; null unless unled.</summary>
    public int? UnledMaxSize { get; init; }
}
