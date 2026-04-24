namespace AscendantEmpire.Application.Features.TroopOrders.Helpers;

public interface IPathFinder
{
    PathResult? FindShortestPath(PathMap map, BaseCoord start, BaseCoord dest, decimal movesPerTurn);
}
