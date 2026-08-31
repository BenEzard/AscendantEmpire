using System;
using System.Collections.Generic;

namespace AscendantEmpire.Application.Features.ForceOrders.Helpers;

public readonly record struct BaseCoord(int X, int Y);

public enum TileBoundary
{
    North,
    East,
    South,
    West
}

public readonly record struct PathTile(
    decimal Cost,
    bool IsImpassable = false
);

public enum TileBoundaryType
{
    Open,
    Blocked,
    TerrainFeature
}

public readonly record struct TileBoundaryRule(
    TileBoundaryType Type = TileBoundaryType.Open,
    decimal AdditionalMovementCost = 0m)
{
    public bool IsBlocked => Type == TileBoundaryType.Blocked;
}

public readonly record struct TileBoundaryKey(BaseCoord Coord, TileBoundary Boundary);

public sealed class PathMap
{
    public required PathTile[][] Tiles { get; init; }
    public bool HasHorizontalWrapping { get; init; }
    public bool HasVerticalWrapping { get; init; }
    public IReadOnlyDictionary<TileBoundaryKey, TileBoundaryRule> Boundaries { get; init; } = new Dictionary<TileBoundaryKey, TileBoundaryRule>();
}

public sealed class PathResult
{
    public Stack<PathStep> ShortestPath { get; init; } = new();
    public int TurnsToDestination { get; init; }
}

public readonly record struct PathStep(BaseCoord Coord, int? ExpectedArrivalTurn);

internal readonly record struct MovementEvaluation(bool IsBlocked, decimal AdditionalMovementCost);

public class PathFinder : IPathFinder
{
    public PathResult? FindShortestPath(
        PathMap map,
        BaseCoord start,
        BaseCoord dest,
        decimal movesPerTurn)
    {
        if (map is null || map.Tiles is null || map.Tiles.Length == 0)
        {
            return null;
        }

        if (movesPerTurn <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(movesPerTurn), "movesPerTurn must be greater than 0.");
        }

        int width = map.Tiles.Length;
        int height = map.Tiles[0]?.Length ?? 0;

        if (height == 0)
        {
            return null;
        }

        for (int x = 0; x < width; x++)
        {
            if (map.Tiles[x] is null || map.Tiles[x].Length != height)
            {
                throw new ArgumentException("Map must be rectangular and contain no null columns.", nameof(map));
            }
        }

        if (!IsInBounds(start, width, height) || !IsInBounds(dest, width, height))
        {
            return null;
        }

        if (IsImpassable(map, start) || IsImpassable(map, dest))
        {
            return null;
        }

        ValidateBoundaryRules(map, width, height);

        if (start == dest)
        {
            return new PathResult
            {
                ShortestPath = new Stack<PathStep>(new[] { new PathStep(start, 0) }),
                TurnsToDestination = 0
            };
        }

        decimal[,] distances = new decimal[width, height];
        bool[,] visited = new bool[width, height];
        BaseCoord?[,] previous = new BaseCoord?[width, height];

        for (int x = 0; x < width; x++)
        {
            for (int y = 0; y < height; y++)
            {
                distances[x, y] = decimal.MaxValue;
            }
        }

        distances[start.X, start.Y] = 0m;

        PriorityQueue<BaseCoord, decimal> queue = new();
        queue.Enqueue(start, 0m);

        int[] dx = { -1, 0, 1, -1, 1, -1, 0, 1 };
        int[] dy = { -1, -1, -1, 0, 0, 1, 1, 1 };

        while (queue.Count > 0)
        {
            BaseCoord current = queue.Dequeue();

            if (visited[current.X, current.Y])
            {
                continue;
            }

            visited[current.X, current.Y] = true;

            if (current == dest)
            {
                break;
            }

            for (int i = 0; i < dx.Length; i++)
            {
                if (!TryGetNeighbor(map, current, dx[i], dy[i], out BaseCoord next))
                {
                    continue;
                }

                if (next == current)
                {
                    continue;
                }

                if (IsImpassable(map, next))
                {
                    continue;
                }

                MovementEvaluation movement = EvaluateMove(map, current, next, dx[i], dy[i]);
                if (movement.IsBlocked)
                {
                    continue;
                }

                if (visited[next.X, next.Y])
                {
                    continue;
                }

                decimal newDistance = distances[current.X, current.Y] + GetTile(map, next).Cost + movement.AdditionalMovementCost;

                if (newDistance < distances[next.X, next.Y])
                {
                    distances[next.X, next.Y] = newDistance;
                    previous[next.X, next.Y] = current;
                    queue.Enqueue(next, newDistance);
                }
            }
        }

        if (distances[dest.X, dest.Y] == decimal.MaxValue)
        {
            return null;
        }

        Stack<PathStep> path = BuildPath(previous, map, start, dest, movesPerTurn);
        int turns = (int)decimal.Ceiling(distances[dest.X, dest.Y] / movesPerTurn);

        return new PathResult
        {
            ShortestPath = path,
            TurnsToDestination = turns
        };
    }

    private static void ValidateBoundaryRules(PathMap map, int width, int height)
    {
        foreach ((TileBoundaryKey key, TileBoundaryRule rule) in map.Boundaries)
        {
            if (!IsInBounds(key.Coord, width, height))
            {
                throw new ArgumentException($"Boundary definition for tile ({key.Coord.X}, {key.Coord.Y}) is out of bounds.", nameof(map));
            }

            if (rule.AdditionalMovementCost < 0)
            {
                throw new ArgumentException("Boundary movement costs cannot be negative.", nameof(map));
            }
        }
    }

    private static MovementEvaluation EvaluateMove(PathMap map, BaseCoord current, BaseCoord next, int deltaX, int deltaY)
    {
        bool isDiagonal = Math.Abs(deltaX) == 1 && Math.Abs(deltaY) == 1;
        return isDiagonal
            ? EvaluateDiagonalMove(map, current, deltaX, deltaY)
            : EvaluateOrthogonalMove(map, current, next, deltaX, deltaY);
    }

    private static MovementEvaluation EvaluateOrthogonalMove(PathMap map, BaseCoord current, BaseCoord next, int deltaX, int deltaY)
    {
        TileBoundary currentBoundary = GetBoundary(deltaX, deltaY);
        TileBoundary nextBoundary = GetOppositeBoundary(currentBoundary);

        // A shared edge may be described on either adjacent tile. When both sides are supplied,
        // they must match so the same river or wall is represented consistently from both tiles.
        TileBoundaryRule rule = ResolveBoundaryRule(map, current, currentBoundary, next, nextBoundary);

        return new MovementEvaluation(
            IsBlocked: rule.IsBlocked,
            AdditionalMovementCost: rule.IsBlocked ? 0m : rule.AdditionalMovementCost);
    }

    private static MovementEvaluation EvaluateDiagonalMove(PathMap map, BaseCoord current, int deltaX, int deltaY)
    {
        // Diagonal movement is evaluated using the two orthogonal components that compose it.
        // For edge rules we apply the most restrictive interpretation: if either component edge
        // is blocked, the diagonal is blocked. If both are passable, the larger additional cost
        // wins because it is the stricter of the two movement penalties.
        bool hasHorizontalNeighbor = TryGetNeighbor(map, current, deltaX, 0, out BaseCoord horizontalNeighbor);
        bool hasVerticalNeighbor = TryGetNeighbor(map, current, 0, deltaY, out BaseCoord verticalNeighbor);

        bool horizontalTerrainBlocked = !hasHorizontalNeighbor || IsImpassable(map, horizontalNeighbor);
        bool verticalTerrainBlocked = !hasVerticalNeighbor || IsImpassable(map, verticalNeighbor);

        MovementEvaluation horizontalBoundary = hasHorizontalNeighbor && !horizontalTerrainBlocked
            ? EvaluateOrthogonalMove(map, current, horizontalNeighbor, deltaX, 0)
            : default;

        MovementEvaluation verticalBoundary = hasVerticalNeighbor && !verticalTerrainBlocked
            ? EvaluateOrthogonalMove(map, current, verticalNeighbor, 0, deltaY)
            : default;

        if (horizontalBoundary.IsBlocked || verticalBoundary.IsBlocked)
        {
            return new MovementEvaluation(IsBlocked: true, AdditionalMovementCost: 0m);
        }

        // We keep the previous corner-cutting rule for impassable side tiles: a diagonal is only
        // rejected when both orthogonal side tiles are unavailable.
        if (horizontalTerrainBlocked && verticalTerrainBlocked)
        {
            return new MovementEvaluation(IsBlocked: true, AdditionalMovementCost: 0m);
        }

        return new MovementEvaluation(
            IsBlocked: false,
            AdditionalMovementCost: Math.Max(horizontalBoundary.AdditionalMovementCost, verticalBoundary.AdditionalMovementCost));
    }

    private static bool TryGetNeighbor(PathMap map, BaseCoord current, int deltaX, int deltaY, out BaseCoord next)
    {
        int width = map.Tiles.Length;
        int height = map.Tiles[0].Length;

        int nextX = current.X + deltaX;
        int nextY = current.Y + deltaY;

        if (nextX < 0 || nextX >= width)
        {
            if (!map.HasHorizontalWrapping)
            {
                next = default;
                return false;
            }

            nextX = Mod(nextX, width);
        }

        if (nextY < 0 || nextY >= height)
        {
            if (!map.HasVerticalWrapping)
            {
                next = default;
                return false;
            }

            nextY = Mod(nextY, height);
        }

        next = new BaseCoord(nextX, nextY);
        return true;
    }

    private static int Mod(int value, int modulo)
    {
        return ((value % modulo) + modulo) % modulo;
    }

    private static PathTile GetTile(PathMap map, BaseCoord coord)
    {
        return map.Tiles[coord.X][coord.Y];
    }

    private static TileBoundaryRule ResolveBoundaryRule(
        PathMap map,
        BaseCoord current,
        TileBoundary currentBoundary,
        BaseCoord next,
        TileBoundary nextBoundary)
    {
        bool hasCurrentRule = map.Boundaries.TryGetValue(new TileBoundaryKey(current, currentBoundary), out TileBoundaryRule currentRule);
        bool hasNextRule = map.Boundaries.TryGetValue(new TileBoundaryKey(next, nextBoundary), out TileBoundaryRule nextRule);

        if (hasCurrentRule && hasNextRule && currentRule != nextRule)
        {
            throw new ArgumentException(
                $"Boundary mismatch between ({current.X}, {current.Y}) {currentBoundary} and ({next.X}, {next.Y}) {nextBoundary}.",
                nameof(map));
        }

        if (hasCurrentRule)
        {
            return currentRule;
        }

        if (hasNextRule)
        {
            return nextRule;
        }

        return default;
    }

    private static TileBoundary GetBoundary(int deltaX, int deltaY)
    {
        return (deltaX, deltaY) switch
        {
            (0, -1) => TileBoundary.North,
            (1, 0) => TileBoundary.East,
            (0, 1) => TileBoundary.South,
            (-1, 0) => TileBoundary.West,
            _ => throw new ArgumentOutOfRangeException(nameof(deltaX), "Boundary lookup requires an orthogonal move.")
        };
    }

    private static TileBoundary GetOppositeBoundary(TileBoundary boundary)
    {
        return boundary switch
        {
            TileBoundary.North => TileBoundary.South,
            TileBoundary.East => TileBoundary.West,
            TileBoundary.South => TileBoundary.North,
            TileBoundary.West => TileBoundary.East,
            _ => throw new ArgumentOutOfRangeException(nameof(boundary))
        };
    }

    private static bool IsImpassable(PathMap map, BaseCoord coord)
    {
        return GetTile(map, coord).IsImpassable;
    }

    private static bool IsInBounds(BaseCoord coord, int width, int height)
    {
        return coord.X >= 0
            && coord.X < width
            && coord.Y >= 0
            && coord.Y < height;
    }

    private static Stack<PathStep> BuildPath(BaseCoord?[,] previous, PathMap map, BaseCoord start, BaseCoord dest, decimal movesPerTurn)
    {
        List<BaseCoord> coords = new();
        BaseCoord current = dest;

        while (true)
        {
            coords.Add(current);

            if (current == start)
            {
                break;
            }

            BaseCoord? prev = previous[current.X, current.Y];
            if (prev is null)
            {
                throw new InvalidOperationException("Path reconstruction failed.");
            }

            current = prev.Value;
        }

        coords.Reverse();

        List<PathStep> steps = new(coords.Count)
        {
            new PathStep(coords[0], 0)
        };

        decimal cumulativeCost = 0m;

        for (int i = 1; i < coords.Count; i++)
        {
            // Reconstructing turn markers must use the same movement accounting as Dijkstra.
            // That means each path segment includes both the destination tile cost and any
            // additional boundary cost from the edge that was crossed to reach that tile.
            cumulativeCost += GetStepCost(map, coords[i - 1], coords[i]);

            int currentTurn = (int)decimal.Ceiling(cumulativeCost / movesPerTurn);
            int? expectedArrivalTurn = null;

            if (i == coords.Count - 1)
            {
                expectedArrivalTurn = currentTurn;
            }
            else
            {
                decimal nextCumulativeCost = cumulativeCost + GetStepCost(map, coords[i], coords[i + 1]);
                int nextTurn = (int)decimal.Ceiling(nextCumulativeCost / movesPerTurn);

                if (nextTurn > currentTurn)
                {
                    expectedArrivalTurn = currentTurn;
                }
            }

            steps.Add(new PathStep(coords[i], expectedArrivalTurn));
        }

        Stack<PathStep> stack = new();
        for (int i = steps.Count - 1; i >= 0; i--)
        {
            stack.Push(steps[i]);
        }

        return stack;
    }

    private static decimal GetStepCost(PathMap map, BaseCoord current, BaseCoord next)
    {
        (int deltaX, int deltaY) = ResolveDelta(map, current, next);
        MovementEvaluation movement = EvaluateMove(map, current, next, deltaX, deltaY);

        if (movement.IsBlocked)
        {
            throw new InvalidOperationException("Path reconstruction encountered a blocked movement step.");
        }

        return GetTile(map, next).Cost + movement.AdditionalMovementCost;
    }

    private static (int DeltaX, int DeltaY) ResolveDelta(PathMap map, BaseCoord current, BaseCoord next)
    {
        int width = map.Tiles.Length;
        int height = map.Tiles[0].Length;

        int deltaX = next.X - current.X;
        int deltaY = next.Y - current.Y;

        // Wrapped paths store absolute coordinates, so a wrapped step such as X=0 to X=width-1
        // must be converted back to the logical single-tile move (-1) before movement rules are
        // re-evaluated for turn markers and path costing.
        if (map.HasHorizontalWrapping)
        {
            if (deltaX == width - 1)
            {
                deltaX = -1;
            }
            else if (deltaX == -(width - 1))
            {
                deltaX = 1;
            }
        }

        if (map.HasVerticalWrapping)
        {
            if (deltaY == height - 1)
            {
                deltaY = -1;
            }
            else if (deltaY == -(height - 1))
            {
                deltaY = 1;
            }
        }

        if (Math.Abs(deltaX) > 1 || Math.Abs(deltaY) > 1 || (deltaX == 0 && deltaY == 0))
        {
            throw new InvalidOperationException("Path reconstruction encountered non-adjacent coordinates.");
        }

        return (deltaX, deltaY);
    }
}
