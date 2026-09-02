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
}
