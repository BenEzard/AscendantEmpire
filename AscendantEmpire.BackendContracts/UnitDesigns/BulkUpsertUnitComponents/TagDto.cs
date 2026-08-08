using AscendantEmpire.Domain.Enums.Maps;
using AscendantEmpire.Domain.Enums.UnitDesigns;

namespace AscendantEmpire.BackendContracts.UnitDesigns.BulkUpsertUnitComponents;

/// <summary>
/// A global, reusable tag definition that components can reference by <see cref="Code"/>.
/// </summary>
/// <remarks>
/// Carries the military-organisation fields as well as the tag's own catalogue fields, so an exported
/// catalogue re-imports with its organisation data intact — without them a hand-authored unled tag would
/// arrive in the target database silently commissioned.
/// </remarks>
public record TagDto
{
    /// <summary>
    /// Unique, stable key for the tag, e.g. "amphibious", "stealth".
    /// </summary>
    public required string Code { get; init; }

    public required string DisplayName { get; init; }

    /// <summary>
    /// Optional free-text description.
    /// </summary>
    public string? Description { get; init; }

    /// <summary>
    /// Whether this tag is a structural classification or a bonus (buff/perk).
    /// </summary>
    public required TagType TagType { get; init; }

    /// <summary>
    /// The movement domain, for a MilitaryOrganisationalGroup tag. Must be null for any other tag type.
    /// </summary>
    public DomainType? MovementDomain { get; init; }

    /// <summary>
    /// The leadership model, for a MilitaryOrganisationalGroup tag. Must be null for any other tag type.
    /// </summary>
    public LeadershipModel? LeadershipModel { get; init; }

    /// <summary>
    /// The indivisible group size (quantum) for an Unled tag. Required when <see cref="LeadershipModel"/>
    /// is Unled; must be null otherwise.
    /// </summary>
    public int? UnledBaseSize { get; init; }

    /// <summary>
    /// The size an unled unit takes when created without an explicit choice. A multiple of
    /// <see cref="UnledBaseSize"/>. Required when <see cref="LeadershipModel"/> is Unled; must be null otherwise.
    /// </summary>
    public int? UnledDefaultSize { get; init; }

    /// <summary>
    /// The largest a single unled unit may be. Required when <see cref="LeadershipModel"/> is Unled; must be
    /// null otherwise.
    /// </summary>
    public int? UnledMaxSize { get; init; }
}
