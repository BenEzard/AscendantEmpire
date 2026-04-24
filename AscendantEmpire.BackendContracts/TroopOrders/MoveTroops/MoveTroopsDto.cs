using AscendantEmpire.FrontendContracts.TroopOrders.MoveTroops;

namespace AscendantEmpire.BackendContracts.TroopOrders.MoveTroops;

public record class MoveTroopsDto : IMoveTroopsDto
{
    public int GameId { get; init; }
    public int PlayerId { get; init; }
    public int TroopInstanceId { get; init; }
    public int XCoord { get; init; }
    public int YCoord { get; init; }
}
