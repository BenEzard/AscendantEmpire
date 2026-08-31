using AscendantEmpire.FrontendContracts.ForceOrders.MoveForces;

namespace AscendantEmpire.BackendContracts.ForceOrders.MoveForces;

public record class MoveForcesDto : IMoveForcesDto
{
    public int UnitInstanceId { get; init; }
    public int XCoord { get; init; }
    public int YCoord { get; init; }
}
