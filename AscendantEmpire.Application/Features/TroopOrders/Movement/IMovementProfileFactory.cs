using AscendantEmpire.Domain.Entities.Units;
using AscendantEmpire.Domain.Enums.Maps;

namespace AscendantEmpire.Application.Features.TroopOrders.Movement;

public interface IMovementProfileFactory
{
    MovementProfile Create(UnitInstance troop, DomainType domainType);
    MovementState CreateState(UnitInstance troop);
}
