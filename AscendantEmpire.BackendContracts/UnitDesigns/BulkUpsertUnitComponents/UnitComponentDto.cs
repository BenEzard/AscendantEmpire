using AscendantEmpire.Domain.Enums.UnitDesigns;

namespace AscendantEmpire.BackendContracts.UnitDesigns.BulkUpsertUnitComponents;

/// <summary>
/// A single buildable component to insert into the global catalogue, with its type-specific extension.
/// </summary>
/// <remarks>
/// Exactly one of <see cref="Body"/>, <see cref="Weapon"/> or <see cref="Armour"/> must be populated
/// for the corresponding <see cref="ComponentType"/>; other component types carry none of the three.
/// </remarks>
public record UnitComponentDto
{
    /// <summary>
    /// Unique, stable key for the component, e.g. "infantry_body", "rifle_mk1". Used to detect duplicates.
    /// </summary>
    public required string Code { get; init; }

    public required string DisplayName { get; init; }

    /// <summary>
    /// Optional free-text description.
    /// </summary>
    public string? Description { get; init; }

    /// <summary>
    /// Optional path to the image shown for this component in the dev Entity Setup Tool.
    /// </summary>
    public string? DesignerImagePath { get; init; }

    public required ComponentType ComponentType { get; init; }

    /// <summary>
    /// The weight of the component.
    /// </summary>
    public long Weight { get; init; }

    /// <summary>
    /// How far this component lets a Unit see terrain and buildings (fixed-point, Scale 1,000). Null = no contribution.
    /// </summary>
    public long? Sight { get; init; }

    /// <summary>
    /// How well this component detects other Units (fixed-point, Scale 1,000). Null = no contribution.
    /// </summary>
    public long? Detection { get; init; }

    /// <summary>
    /// How easily this component is seen by other Units (fixed-point, Scale 1,000). Null = no contribution.
    /// </summary>
    public long? Visibility { get; init; }

    /// <summary>
    /// Body-only extension; populated only when <see cref="ComponentType"/> is <c>Body</c>.
    /// </summary>
    public UnitComponentBodyDto? Body { get; init; }

    /// <summary>
    /// Weapon-only extension; populated only when <see cref="ComponentType"/> is <c>Weapon</c>.
    /// </summary>
    public UnitComponentWeaponDto? Weapon { get; init; }

    /// <summary>
    /// Armour-only extension; populated only when <see cref="ComponentType"/> is <c>Armour</c>.
    /// </summary>
    public UnitComponentArmourDto? Armour { get; init; }

    /// <summary>
    /// Codes of buff tags attached to this component. Each must resolve to a tag in the payload
    /// <c>Tags</c> list or an existing tag in the database.
    /// </summary>
    public IReadOnlyCollection<string> TagCodes { get; init; } = [];
}
