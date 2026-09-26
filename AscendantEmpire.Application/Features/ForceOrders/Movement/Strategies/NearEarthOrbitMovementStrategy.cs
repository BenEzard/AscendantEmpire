using AscendantEmpire.Domain.Enums.Maps;

namespace AscendantEmpire.Application.Features.ForceOrders.Movement.Strategies;

/// <summary>
/// Orbital Forces pass above the map entirely, so no terrain refuses them.
/// </summary>
/// <remarks>
/// Same reasoning as <see cref="AirMovementStrategy"/>. Orbital movement will eventually follow its own rules rather than the tile grid, but until
/// there is a design for that it behaves as flight does.
/// </remarks>
internal sealed class NearEarthOrbitMovementStrategy : IMovementDomainStrategy
{
    public DomainType MovementDomain => DomainType.NearEarthOrbit;

    public string? GetEntryRefusal(MovementTargetTile target)
    {
        ArgumentNullException.ThrowIfNull(target);

        return null;
    }

    /// <remarks>Rivers are no obstacle from orbit, so the step is judged on the tile alone, as flight is.</remarks>
    public string? GetStepRefusal(MovementStep step)
    {
        ArgumentNullException.ThrowIfNull(step);

        return GetEntryRefusal(step.Target);
    }

    /// <remarks>The tile's cost is the whole price, as it is for flight.</remarks>
    public decimal GetStepCost(MovementStep step)
    {
        ArgumentNullException.ThrowIfNull(step);

        return step.Target.MovementCost;
    }
}
