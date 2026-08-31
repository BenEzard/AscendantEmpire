using AscendantEmpire.Domain.Enums.Troops;

namespace AscendantEmpire.BackendContracts.TroopOrders.GetTroopOrders;

public record TroopOrderParticipant
{
    public int UnitInstanceId { get; init; }

    public TroopOrderParticipantRole Role { get; init; }
}
