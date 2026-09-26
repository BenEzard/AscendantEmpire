using AscendantEmpire.Domain.Enums.Maps;

namespace AscendantEmpire.Application.Features.ForceOrders.Movement.Strategies;

/// <summary>
/// Surface water Forces cross water, and may leave it only to sail up a Large river.
/// </summary>
/// <remarks>
/// The mirror of <see cref="LandMovementStrategy"/>. As a tile question (<see cref="GetEntryRefusal"/>, which fog of war asks) land is refused. As a
/// step question (<see cref="GetStepRefusal"/>) a ship may enter a bank tile by sailing up a Large river. Beaching a ship on an ordinary coast is a landing,
/// not a move, and will be its own action once ports and amphibious transport exist.
/// </remarks>
internal sealed class WaterMovementStrategy : IMovementDomainStrategy
{
    public DomainType MovementDomain => DomainType.Water;

    public string? GetEntryRefusal(MovementTargetTile target)
    {
        ArgumentNullException.ThrowIfNull(target);

        if (!target.IsWater)
        {
            return "Cannot enter land.";
        }

        return null;
    }

    /// <remarks>
    /// A step may take a ship onto land only to sail up a Large river, which is why this differs from <see cref="GetEntryRefusal"/>. See
    /// <see cref="RiverMovementRules.GetSailingRefusal"/>.
    /// </remarks>
    public string? GetStepRefusal(MovementStep step)
    {
        return RiverMovementRules.GetSailingRefusal(step);
    }

    public decimal GetStepCost(MovementStep step)
    {
        return RiverMovementRules.GetSailingStepCost(step);
    }
}
