using AscendantEmpire.Domain.Enums.Maps;
using AscendantEmpire.Domain.Enums.Modifiers;

namespace AscendantEmpire.Application.Features.ForceOrders.Movement;

/// <summary>
/// Immutable movement characteristics of a troop's unit design.
/// This model describes what the unit is capable of in general and is safe to reuse across
/// multiple path evaluations.
/// </summary>
public sealed record MovementProfile
{
    /// <summary>
    /// Primary strategy selector. Land, Water, Air, etc.
    /// </summary>
    public required DomainType DomainType { get; init; }

    /// <summary>
    /// The amount of movement regained each turn by this unit design.
    /// Examples: 1, 2, 2.2 or 4.
    /// </summary>
    public required decimal BaseMovementPoints { get; init; }

    /// <summary>
    /// The maximum amount of movement a unit can hold after turn refresh.
    /// Fractional movement accumulates over time, so the cap is the ceiling of the base value.
    /// </summary>
    public int MaxMovementPointsPerTurn => (int)decimal.Ceiling(BaseMovementPoints);

    /// <summary>
    /// Fast lookup set for common movement bonuses.
    /// </summary>
    public IReadOnlySet<MoveModifier> Bonuses { get; init; } = new HashSet<MoveModifier>();

    /// <summary>
    /// Richer JSON-derived modifiers that strategies can interpret for more advanced rules.
    /// </summary>
    public IReadOnlyList<MovementModifier> Modifiers { get; init; } = [];

    public bool HasBonus(MoveModifier bonus)
    {
        return Bonuses.Contains(bonus);
    }
}
