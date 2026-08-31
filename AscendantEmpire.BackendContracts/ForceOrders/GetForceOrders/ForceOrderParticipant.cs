using AscendantEmpire.Domain.Enums.Forces;

namespace AscendantEmpire.BackendContracts.ForceOrders.GetForceOrders;

public record ForceOrderParticipant
{
    public int UnitInstanceId { get; init; }

    public ForceOrderParticipantRole Role { get; init; }
}
