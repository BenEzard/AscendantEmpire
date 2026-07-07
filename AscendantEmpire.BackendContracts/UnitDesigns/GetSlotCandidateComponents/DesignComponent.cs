using AscendantEmpire.Domain.Enums.UnitDesigns;

namespace AscendantEmpire.BackendContracts.UnitDesigns.GetSlotCandidateComponents;

/// <summary>
/// A non-body component the player has unlocked that fits a given slot — returned to populate the
/// component-selection window with full display stats.
/// </summary>
/// <remarks>
/// <see cref="Weapon"/>/<see cref="Armour"/> are populated only for the matching <see cref="ComponentType"/>;
/// other types (equipment, engine, radar, …) carry neither and are described by their tags/stats alone.
/// </remarks>
public record DesignComponent
{
    public required int Id { get; init; }
    public required string Code { get; init; }
    public required string DisplayName { get; init; }
    public string? Description { get; init; }
    public required ComponentType ComponentType { get; init; }
    public required long Weight { get; init; }

    /// <summary>How well the component detects other Units (fixed-point, Scale 1,000). Null = no contribution.</summary>
    public long? Detection { get; init; }

    /// <summary>How easily the component is seen (fixed-point, Scale 1,000). Null = no contribution.</summary>
    public long? Visibility { get; init; }

    public DesignWeapon? Weapon { get; init; }
    public DesignArmour? Armour { get; init; }

    public required IReadOnlyCollection<string> TagCodes { get; init; }
}
