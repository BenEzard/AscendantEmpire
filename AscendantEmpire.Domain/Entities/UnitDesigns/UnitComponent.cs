using AscendantEmpire.Domain.Enums.UnitDesigns;

using System.ComponentModel.DataAnnotations;

namespace AscendantEmpire.Domain.Entities.UnitDesigns;

/// <summary>
/// A single buildable part in the global component catalog (body, weapon, engine, armour, etc.).
/// </summary>
/// <remarks>
/// <para>This is the shared global reference list — rows are not owned by any player or game.</para>
/// <para>Body components carry extra properties in the 1:1 <see cref="UnitComponentBody"/> extension.</para>
/// </remarks>
public class UnitComponent
{
    public const int CODE_MAX_LENGTH = 50;
    public const int DISPLAY_NAME_MAX_LENGTH = 100;

    public int Id { get; set; }

    /// <summary>
    /// Unique, stable key for the component, e.g. "infantry_body", "rifle_mk1".
    /// </summary>
    [MaxLength(CODE_MAX_LENGTH)]
    public required string Code { get; set; }

    [MaxLength(DISPLAY_NAME_MAX_LENGTH)]
    public required string DisplayName { get; set; }

    /// <summary>
    /// Optional free-text description.
    /// </summary>
    public string? Description { get; set; }

    public required ComponentType ComponentType { get; set; }

    /// <summary>
    /// The weight of the component.
    /// </summary>
    public long Weight { get; set; }

    /// <summary>
    /// How well this component detects other Units. Null means the component contributes no detection.
    /// </summary>
    /// <remarks>
    /// Raw fixed-point (Scale 1,000) like weapon damage — a stored value of 1,000 represents 1.0.
    /// Detection values from a Unit's fitted components are aggregated to the Unit's overall sight.
    /// </remarks>
    public long? Detection { get; set; }

    /// <summary>
    /// How easily this component is seen by other Units. Null means the component contributes no visibility.
    /// </summary>
    /// <remarks>
    /// Raw fixed-point (Scale 1,000) like weapon damage — a stored value of 1,000 represents 1.0.
    /// Visibility values from a Unit's fitted components are aggregated to the Unit's overall signature.
    /// </remarks>
    public long? Visibility { get; set; }

    /// <summary>
    /// Body-only extension; populated only when <see cref="ComponentType"/> is <c>Body</c>.
    /// </summary>
    public UnitComponentBody? Body { get; set; }

    /// <summary>
    /// Weapon-only extension; populated only when <see cref="ComponentType"/> is <c>Weapon</c>.
    /// </summary>
    public UnitComponentWeapon? Weapon { get; set; }

    /// <summary>
    /// Armour-only extension; populated only when <see cref="ComponentType"/> is <c>Armour</c>.
    /// </summary>
    public UnitComponentArmour? Armour { get; set; }

    /// <summary>
    /// The Template Slots this component exposes for fitting other components (0 or more). Any component
    /// type may carry slots — a fitted component can itself expose slots for further nesting.
    /// </summary>
    public ICollection<UnitComponentSlot> Slots { get; set; } = [];

    /// <summary>
    /// The buff/bonus tags attached to this component (0 or more).
    /// </summary>
    public ICollection<UnitComponentTag> Tags { get; set; } = [];

    /// <summary>
    /// Supersession links where this component is the predecessor — i.e. the newer components that
    /// supersede this one (what supersedes me).
    /// </summary>
    public ICollection<ComponentSupersession> SupersededBy { get; set; } = [];

    /// <summary>
    /// Supersession links where this component is the successor — i.e. the older components this one
    /// supersedes (what I supersede).
    /// </summary>
    public ICollection<ComponentSupersession> Supersedes { get; set; } = [];
}
