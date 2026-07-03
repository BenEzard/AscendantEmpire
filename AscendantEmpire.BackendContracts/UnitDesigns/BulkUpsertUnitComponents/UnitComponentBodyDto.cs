using AscendantEmpire.Domain.Enums.Maps;

namespace AscendantEmpire.BackendContracts.UnitDesigns.BulkUpsertUnitComponents;

/// <summary>
/// Body-specific properties for a component whose type is <c>Body</c>.
/// </summary>
/// <remarks>
/// A body defines movement characteristics and unit-size bounds. Slots now live on the component itself
/// (see <c>UnitComponentDto.Slots</c>), so any component — not just a body — can expose them.
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
    /// Minimum number of individuals in a Unit using this body (the smallest allowed unit size).
    /// </summary>
    public int MinimumUnitSize { get; init; }

    /// <summary>
    /// Maximum number of individuals in a Unit using this body (the largest allowed unit size).
    /// </summary>
    public int MaximumUnitSize { get; init; }

    /// <summary>
    /// The step by which unit size can be adjusted.
    /// </summary>
    public int SizeIncrement { get; init; }
}
