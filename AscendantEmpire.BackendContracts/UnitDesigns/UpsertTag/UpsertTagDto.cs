using AscendantEmpire.Domain.Enums.Maps;
using AscendantEmpire.Domain.Enums.UnitDesigns;

namespace AscendantEmpire.BackendContracts.UnitDesigns.UpsertTag;

/// <summary>
/// The request body for creating (POST) or updating (PUT) a single tag in the global catalog.
/// </summary>
public record UpsertTagDto
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
}
