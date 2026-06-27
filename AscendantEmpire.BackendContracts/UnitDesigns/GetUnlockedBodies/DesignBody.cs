using AscendantEmpire.BackendContracts.UnitDesigns.Shared;
using AscendantEmpire.Domain.Enums.Maps;

namespace AscendantEmpire.BackendContracts.UnitDesigns.GetUnlockedBodies;

/// <summary>
/// A body component the player has unlocked, with its movement/strength stats and the slots (and their
/// rules) other components fit into. This is the canvas the unit designer is built around.
/// </summary>
public record DesignBody
{
    public required int Id { get; init; }
    public required string Code { get; init; }
    public required string DisplayName { get; init; }
    public string? Description { get; init; }
    public required long Weight { get; init; }

    /// <summary>How well the body detects other Units (fixed-point, Scale 1,000). Null = no contribution.</summary>
    public long? Detection { get; init; }

    /// <summary>How easily the body is seen (fixed-point, Scale 1,000). Null = no contribution.</summary>
    public long? Visibility { get; init; }

    public required DomainType MovementDomain { get; init; }
    public required decimal CampaignMovementPoints { get; init; }
    public required decimal BattlefieldMovementPoints { get; init; }
    public required int MinStrength { get; init; }
    public required int MaxStrength { get; init; }
    public required int IncrementStrength { get; init; }

    public required IReadOnlyCollection<string> TagCodes { get; init; }
    public required IReadOnlyCollection<DesignSlot> Slots { get; init; }
}
