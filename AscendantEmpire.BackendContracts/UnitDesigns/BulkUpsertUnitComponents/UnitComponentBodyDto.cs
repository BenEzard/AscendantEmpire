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
    /// Movement Points available per turn on the campaign map.
    /// </summary>
    public decimal CampaignMovementPoints { get; init; }

    /// <summary>
    /// Movement Points available per turn on the battlefield.
    /// </summary>
    public decimal BattlefieldMovementPoints { get; init; }

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
