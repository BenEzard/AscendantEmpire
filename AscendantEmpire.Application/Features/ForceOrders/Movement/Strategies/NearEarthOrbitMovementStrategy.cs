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
}
