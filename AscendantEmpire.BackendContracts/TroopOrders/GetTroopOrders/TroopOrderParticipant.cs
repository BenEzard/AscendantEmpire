using AscendantEmpire.Domain.Enums.Troops;

namespace AscendantEmpire.BackendContracts.TroopOrders.GetTroopOrders;

public record TroopOrderParticipant
{
    public int TroopInstanceId { get; init; }

    public TroopOrderParticipantRole Role { get; init; }
}
