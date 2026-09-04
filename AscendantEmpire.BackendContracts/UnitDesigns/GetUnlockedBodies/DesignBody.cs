using AscendantEmpire.BackendContracts.UnitDesigns.Shared;
using AscendantEmpire.Domain.Enums.Maps;

namespace AscendantEmpire.BackendContracts.UnitDesigns.GetUnlockedBodies;

/// <summary>
/// A body component the player has unlocked, with its movement/unit-size stats and the slots (and their
/// rules) other components fit into. This is the canvas the unit designer is built around.
/// </summary>
public record DesignBody
{
    public required int Id { get; init; }
    public required string Code { get; init; }
    public required string DisplayName { get; init; }
    public string? Description { get; init; }
    public required long Weight { get; init; }

    /// <summary>How far the body lets a Unit see terrain and buildings (fixed-point, Scale 1,000). Null = no contribution.</summary>
    public long? Sight { get; init; }

    /// <summary>How well the body detects other Units (fixed-point, Scale 1,000). Null = no contribution.</summary>
    public long? Detection { get; init; }

    /// <summary>How easily the body is seen (fixed-point, Scale 1,000). Null = no contribution.</summary>
    public long? Visibility { get; init; }

    public required DomainType MovementDomain { get; init; }

    /// <summary>Campaign-map movement points (raw fixed-point, Scale 1,000; 1,000 = 1.0).</summary>
    public required long CampaignMovementPoints { get; init; }

    /// <summary>Battlefield movement points (raw fixed-point, Scale 1,000; 1,000 = 1.0).</summary>
    public required long BattlefieldMovementPoints { get; init; }

    /// <summary>Combat power this body contributes (raw fixed-point, Scale 1,000; 1,000 = 1.0).</summary>
    public required long Power { get; init; }

    /// <summary>Hitpoints (health) this body has (raw fixed-point, Scale 1,000; 1,000 = 1.0).</summary>
    public required long Hitpoints { get; init; }

    public required IReadOnlyCollection<string> TagCodes { get; init; }
    public required IReadOnlyCollection<DesignSlot> Slots { get; init; }
}
