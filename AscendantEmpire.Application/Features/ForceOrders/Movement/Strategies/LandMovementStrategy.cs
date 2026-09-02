using AscendantEmpire.Domain.Enums.Maps;

namespace AscendantEmpire.Application.Features.ForceOrders.Movement.Strategies;

/// <summary>
/// Land Forces may cross dry ground only.
/// </summary>
/// <remarks>
/// Water is refused outright rather than merely made expensive. Crossing it at all will eventually need something to carry the Force — a ford, a
/// bridge, or transport — and none of that exists yet, so there is no case in which a land Force belongs on a water tile today.
/// </remarks>
internal sealed class LandMovementStrategy : IMovementDomainStrategy
{
    public DomainType MovementDomain => DomainType.Land;

    public string? GetEntryRefusal(MovementTargetTile target)
    {
        ArgumentNullException.ThrowIfNull(target);

        if (target.IsWater)
        {
            return $"{target.Describe()} is water, and a land force cannot cross it.";
        }

        return null;
    }
}
