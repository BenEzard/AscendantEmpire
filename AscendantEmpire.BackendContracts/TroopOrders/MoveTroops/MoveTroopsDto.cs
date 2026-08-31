using AscendantEmpire.FrontendContracts.TroopOrders.MoveTroops;

namespace AscendantEmpire.BackendContracts.TroopOrders.MoveTroops;

public record class MoveTroopsDto : IMoveTroopsDto
{
    public int UnitInstanceId { get; init; }
    public int XCoord { get; init; }
    public int YCoord { get; init; }
}
