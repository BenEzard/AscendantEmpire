using AscendantEmpire.Domain.Enums.Maps;

namespace AscendantEmpire.Domain.Entities.UnitDesigns;

/// <summary>
/// Body-specific properties for a <see cref="UnitComponent"/> whose type is <c>Body</c>.
/// </summary>
/// <remarks>
/// One-to-one with <see cref="UnitComponent"/> (shares its primary key). A body defines the movement
/// characteristics, and is the only component type that exposes Template Slots (see <see cref="Slots"/>).
/// </remarks>
public class UnitComponentBody
{
    /// <summary>
    /// One-to-one with <see cref="UnitComponent"/>; also the primary key.
    /// </summary>
    public int UnitComponentId { get; set; }

    /// <summary>
    /// The Domain the unit moves in (land, sea, air, near-Earth orbit).
    /// </summary>
    public DomainType MovementDomain { get; set; }

    /// <summary>
    /// Movement Points available per turn on the campaign map.
    /// </summary>
    /// <remarks>
    /// Stored as a raw fixed-point integer using the Scale 1,000 convention of <c>ScaledAmount</c>
    /// (1,000 raw = 1.0). Wrap via <c>ScaledAmount.FromRaw(...)</c> to do arithmetic.
    /// </remarks>
    public long CampaignMovementPoints { get; set; }

    /// <summary>
    /// Movement Points available per turn on the battlefield.
    /// </summary>
    /// <remarks>
    /// Stored as a raw fixed-point integer using the Scale 1,000 convention of <c>ScaledAmount</c>
    /// (1,000 raw = 1.0). Wrap via <c>ScaledAmount.FromRaw(...)</c> to do arithmetic.
    /// </remarks>
    public long BattlefieldMovementPoints { get; set; }

    /// <summary>
    /// The combat power this body contributes.
    /// </summary>
    /// <remarks>
    /// Stored as a raw fixed-point integer using the Scale 1,000 convention of <c>ScaledAmount</c>
    /// (1,000 raw = 1.0). Wrap via <c>ScaledAmount.FromRaw(...)</c> to do arithmetic.
    /// </remarks>
    public long Power { get; set; }

    /// <summary>
    /// The hitpoints (health) this body has.
    /// </summary>
    /// <remarks>
    /// Stored as a raw fixed-point integer using the Scale 1,000 convention of <c>ScaledAmount</c>
    /// (1,000 raw = 1.0). Wrap via <c>ScaledAmount.FromRaw(...)</c> to do arithmetic.
    /// </remarks>
    public long Hitpoints { get; set; }

    /// <summary>
    /// The Template Slots this body exposes for fitting other components (0 or more). Only bodies expose
    /// slots — a Unit Template is built around a body, and every other component is fitted into its slots.
    /// </summary>
    public ICollection<UnitComponentSlot> Slots { get; set; } = [];

    public UnitComponent UnitComponent { get; set; } = null!;
}
