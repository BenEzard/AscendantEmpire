using AscendantEmpire.Domain.Enums.Maps;

namespace AscendantEmpire.Application.Features.ForceOrders.Movement.Strategies;

/// <summary>
/// Submerged Forces may cross water only.
/// </summary>
/// <remarks>
/// <para>
/// Identical to <see cref="WaterMovementStrategy"/> for now, and registered separately rather than folded into it because without a strategy of
/// its own a template carrying <see cref="DomainType.BelowWater"/> would have no rule to be judged against and its every move would be refused.
/// </para>
/// <para>
/// TODO: submarines should not be able to enter shallow water freely, and depth ought to affect what can see them. Neither the design docs nor the
/// map data say anything about that yet, so this deliberately stops at the surface rule rather than inventing one.
/// </para>
/// </remarks>
internal sealed class BelowWaterMovementStrategy : IMovementDomainStrategy
{
    public DomainType MovementDomain => DomainType.BelowWater;

    public string? GetEntryRefusal(MovementTargetTile target)
    {
        ArgumentNullException.ThrowIfNull(target);

        if (!target.IsWater)
        {
            return "Cannot enter land.";
        }

        return null;
    }

    /// <remarks>Sails up Large rivers exactly as a surface ship does. See <see cref="RiverMovementRules.GetSailingRefusal"/>.</remarks>
    public string? GetStepRefusal(MovementStep step)
    {
        return RiverMovementRules.GetSailingRefusal(step);
    }

    public decimal GetStepCost(MovementStep step)
    {
        return RiverMovementRules.GetSailingStepCost(step);
    }
}
