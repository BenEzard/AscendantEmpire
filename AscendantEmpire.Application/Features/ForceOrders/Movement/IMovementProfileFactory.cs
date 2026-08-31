using AscendantEmpire.Domain.Entities.Units;
using AscendantEmpire.Domain.Enums.Maps;

namespace AscendantEmpire.Application.Features.ForceOrders.Movement;

public interface IMovementProfileFactory
{
    MovementProfile Create(UnitInstance force, DomainType domainType);
    MovementState CreateState(UnitInstance force);
}
