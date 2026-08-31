namespace AscendantEmpire.Application.Features.ForceOrders.Helpers;

public interface IPathFinder
{
    PathResult? FindShortestPath(PathMap map, BaseCoord start, BaseCoord dest, decimal movesPerTurn);
}
