using AscendantEmpire.Domain.Enums.Maps;
using AscendantEmpire.FrontendContracts.ForceOrders.StepForce;

namespace AscendantEmpire.BackendContracts.ForceOrders.StepForce;

public record class StepForceDto : IStepForceDto
{
    public int UnitInstanceId { get; init; }
    public MoveDirection Direction { get; init; }
}
