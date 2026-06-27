using AscendantEmpire.Domain.Enums.Maps;

namespace AscendantEmpire.Domain.Entities.UnitDesigns;

/// <summary>
/// Body-specific properties for a <see cref="UnitComponent"/> whose type is <c>Body</c>.
/// </summary>
/// <remarks>
/// One-to-one with <see cref="UnitComponent"/> (shares its primary key). A body defines the movement
/// characteristics and strength bounds. Slots now live on <see cref="UnitComponent"/> itself, so any
/// component (not just a body) can expose them.
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

    public UnitComponent UnitComponent { get; set; } = null!;
}
