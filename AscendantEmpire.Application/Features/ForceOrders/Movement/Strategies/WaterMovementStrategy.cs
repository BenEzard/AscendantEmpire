using AscendantEmpire.Domain.Enums.Maps;

namespace AscendantEmpire.Application.Features.ForceOrders.Movement.Strategies;

/// <summary>
/// Surface water Forces may cross water only.
/// </summary>
/// <remarks>
/// The mirror of <see cref="LandMovementStrategy"/>. Beaching a ship to reach a coastal tile is a landing, not a move, and will be its own action
/// once ports and amphibious transport exist.
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
}
