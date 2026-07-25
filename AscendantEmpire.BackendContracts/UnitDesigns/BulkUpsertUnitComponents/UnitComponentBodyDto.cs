using AscendantEmpire.Domain.Enums.Maps;

namespace AscendantEmpire.BackendContracts.UnitDesigns.BulkUpsertUnitComponents;

/// <summary>
/// Body-specific properties for a component whose type is <c>Body</c>.
/// </summary>
/// <remarks>
/// A body defines movement characteristics, and is the only component type that exposes Template Slots
/// (see <see cref="Slots"/>).
/// </remarks>
public record UnitComponentBodyDto
{
    /// <summary>
    /// The Domain the unit moves in (land, sea, air, near-Earth orbit).
    /// </summary>
    public required DomainType MovementDomain { get; init; }

    /// <summary>
    /// Movement Points available per turn on the campaign map (raw fixed-point, Scale 1,000; 1,000 = 1.0).
    /// </summary>
    public long CampaignMovementPoints { get; init; }

    /// <summary>
    /// Movement Points available per turn on the battlefield (raw fixed-point, Scale 1,000; 1,000 = 1.0).
    /// </summary>
    public long BattlefieldMovementPoints { get; init; }

    /// <summary>
    /// The combat power this body contributes (raw fixed-point, Scale 1,000; 1,000 = 1.0).
    /// </summary>
    public long Power { get; init; }

    /// <summary>
    /// The hitpoints (health) this body has (raw fixed-point, Scale 1,000; 1,000 = 1.0).
    /// </summary>
    public long Hitpoints { get; init; }

    /// <summary>
    /// The Template Slots this body exposes for fitting other components (0 or more). Only bodies expose slots.
    /// </summary>
    public IReadOnlyCollection<UnitComponentSlotDto> Slots { get; init; } = [];
}
