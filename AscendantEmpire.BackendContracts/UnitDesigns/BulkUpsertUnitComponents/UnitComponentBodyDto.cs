using AscendantEmpire.Domain.Enums.Maps;

namespace AscendantEmpire.BackendContracts.UnitDesigns.BulkUpsertUnitComponents;

/// <summary>
/// Body-specific properties for a component whose type is <c>Body</c>.
/// </summary>
/// <remarks>
/// A body defines movement characteristics, strength bounds, and the Template Slots that other
/// components fit into.
/// </remarks>
public record UnitComponentBodyDto
{
    /// <summary>
    /// The Domain the unit moves in (land, sea, air, near-Earth orbit).
    /// </summary>
    public required DomainType MovementDomain { get; init; }

    /// <summary>
    /// Movement Points available per turn on the campaign map.
    /// </summary>
    public decimal CampaignMovementPoints { get; init; }

    /// <summary>
    /// Movement Points available per turn on the battlefield.
    /// </summary>
    public decimal BattlefieldMovementPoints { get; init; }

    /// <summary>
    /// Minimum number of individuals in a Unit using this body.
    /// </summary>
    public int MinStrength { get; init; }

    /// <summary>
    /// Maximum number of individuals in a Unit using this body.
    /// </summary>
    public int MaxStrength { get; init; }

    /// <summary>
    /// The size of the increment by which strength can be adjusted.
    /// </summary>
    public int IncrementStrength { get; init; }

    /// <summary>
    /// Optional inherent crush attack the body itself deals (e.g. a cavalry charge). Null = none.
    /// </summary>
    public long? CrushAttack { get; init; }

    /// <summary>
    /// The Template Slots this body exposes for fitting other components (1 or more).
    /// </summary>
    public IReadOnlyCollection<UnitComponentSlotDto> Slots { get; init; } = [];
}
