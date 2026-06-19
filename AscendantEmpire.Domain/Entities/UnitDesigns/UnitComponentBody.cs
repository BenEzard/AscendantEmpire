using AscendantEmpire.Domain.Enums.Maps;

namespace AscendantEmpire.Domain.Entities.UnitDesigns;

/// <summary>
/// Body-specific properties for a <see cref="UnitComponent"/> whose type is <c>Body</c>.
/// </summary>
/// <remarks>
/// One-to-one with <see cref="UnitComponent"/> (shares its primary key). A body defines the
/// movement characteristics, strength bounds, and the Template Slots that other components fit into.
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
    public decimal CampaignMovementPoints { get; set; }

    /// <summary>
    /// Movement Points available per turn on the battlefield.
    /// </summary>
    public decimal BattlefieldMovementPoints { get; set; }

    /// <summary>
    /// Minimum number of individuals in a Unit using this body.
    /// </summary>
    public int MinStrength { get; set; }

    /// <summary>
    /// Maximum number of individuals in a Unit using this body.
    /// </summary>
    public int MaxStrength { get; set; }

    /// <summary>
    /// The size of the increment by which strength can be adjusted (e.g. strength changes in steps of this amount).
    /// </summary>
    public int IncrementStrength { get; set; }

    /// <summary>
    /// Optional inherent crush attack the body itself deals (e.g. a cavalry charge or vehicle ramming).
    /// Null means the body has no inherent attack. Raw fixed-point (Scale 1,000) like weapon damage.
    /// </summary>
    public long? CrushAttack { get; set; }

    public UnitComponent UnitComponent { get; set; } = null!;

    /// <summary>
    /// The Template Slots this body exposes for fitting other components (1 or more).
    /// </summary>
    public ICollection<UnitComponentSlot> Slots { get; set; } = [];
}
