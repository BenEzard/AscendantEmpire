using AscendantEmpire.Domain.Enums.Maps;

namespace AscendantEmpire.Application.Features.ForceOrders.Movement.Strategies;

/// <summary>
/// Air Forces fly over the ground, so no terrain refuses them.
/// </summary>
/// <remarks>
/// They still pay the tile's movement cost, and the handler still refuses an impassable tile before consulting this strategy — a mountain is
/// impassable to everything, and flying over one is a rule this ticket does not attempt. Only the land and water restriction is lifted here.
/// </remarks>
internal sealed class AirMovementStrategy : IMovementDomainStrategy
{
    public DomainType MovementDomain => DomainType.Air;

    public string? GetEntryRefusal(MovementTargetTile target)
    {
        ArgumentNullException.ThrowIfNull(target);

        return null;
    }

    /// <remarks>Rivers are no obstacle to flight, so the step is judged on the tile alone.</remarks>
    public string? GetStepRefusal(MovementStep step)
    {
        ArgumentNullException.ThrowIfNull(step);

        return GetEntryRefusal(step.Target);
    }

    /// <remarks>Nor do rivers speed flight up: the tile's cost is the whole price.</remarks>
    public decimal GetStepCost(MovementStep step)
    {
        ArgumentNullException.ThrowIfNull(step);

        return step.Target.MovementCost;
    }
}
