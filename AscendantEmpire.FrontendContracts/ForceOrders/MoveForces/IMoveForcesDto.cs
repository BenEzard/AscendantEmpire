namespace AscendantEmpire.FrontendContracts.ForceOrders.MoveForces;

/// <summary>
/// The body of a move order: which troop moves and where to.
/// </summary>
/// <remarks>
/// The game and player are deliberately absent — they come from the caller's game token, so a move order cannot be aimed at another player's troops.
/// </remarks>
public interface IMoveForcesDto
{
    int UnitInstanceId { get; }
    int XCoord { get; }
    int YCoord { get; }
}
