using AscendantEmpire.Domain.Enums.Maps;

namespace AscendantEmpire.Domain.Entities.UnitDesigns;

/// <summary>
/// Body-specific properties for a <see cref="UnitComponent"/> whose type is <c>Body</c>.
/// </summary>
/// <remarks>
/// One-to-one with <see cref="UnitComponent"/> (shares its primary key). A body defines the movement
/// characteristics and unit-size bounds. Slots now live on <see cref="UnitComponent"/> itself, so any
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
    /// Minimum number of individuals in a Unit using this body (the smallest allowed unit size).
    /// </summary>
    public int MinimumUnitSize { get; set; }

    /// <summary>
    /// Maximum number of individuals in a Unit using this body (the largest allowed unit size).
    /// </summary>
    public int MaximumUnitSize { get; set; }

    /// <summary>
    /// The step by which unit size can be adjusted (unit size changes in increments of this amount).
    /// </summary>
    public int SizeIncrement { get; set; }

    public UnitComponent UnitComponent { get; set; } = null!;
}
