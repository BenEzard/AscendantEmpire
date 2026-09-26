using AscendantEmpire.Domain.Enums.Maps;

namespace AscendantEmpire.Application.Features.ForceOrders.Movement.Strategies;

/// <summary>
/// Land Forces may cross dry ground only, and cannot cross a Large river.
/// </summary>
/// <remarks>
/// Water is refused outright rather than merely made expensive. Crossing it at all will eventually need something to carry the Force — a ford, a
/// bridge, or transport — and none of that exists yet, so there is no case in which a land Force belongs on a water tile today. The same goes for a
/// Large river between two tiles. A Medium river can be crossed, at a cost.
/// </remarks>
internal sealed class LandMovementStrategy : IMovementDomainStrategy
{
    public DomainType MovementDomain => DomainType.Land;

    public string? GetEntryRefusal(MovementTargetTile target)
    {
        ArgumentNullException.ThrowIfNull(target);

        if (target.IsWater)
        {
            return "Cannot enter water.";
        }

        return null;
    }

    /// <remarks>
    /// On top of the tile rule, a Large river cannot be crossed: nothing carries a Land Force over it until bridges or fords exist. A Medium river can
    /// be crossed, at a cost.
    /// </remarks>
    public string? GetStepRefusal(MovementStep step)
    {
        ArgumentNullException.ThrowIfNull(step);

        string? tileRefusal = GetEntryRefusal(step.Target);

        if (tileRefusal is not null)
        {
            return tileRefusal;
        }

        if (step.Rivers.Crossed == RiverWidth.Large)
        {
            return "Cannot cross a large river.";
        }

        return null;
    }

    /// <remarks>Land Forces are the ones rivers slow down and speed up; see <see cref="RiverMovementRules.GetLandStepCost"/>.</remarks>
    public decimal GetStepCost(MovementStep step)
    {
        return RiverMovementRules.GetLandStepCost(step);
    }
}
