namespace AscendantEmpire.FrontendContracts.TroopOrders.MoveTroops;

public interface IMoveTroopsDto
{
    int GameId { get; }
    int PlayerId { get; }
    int TroopInstanceId { get; }
    int XCoord { get; }
    int YCoord { get; }
}
