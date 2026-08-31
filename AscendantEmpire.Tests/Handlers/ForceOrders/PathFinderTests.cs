using AscendantEmpire.Application.Features.ForceOrders.Helpers;

namespace AscendantEmpire.Tests.Handlers.ForceOrders;

[TestFixture]
public class PathFinderTests
{
    private readonly IPathFinder _pathFinder = new PathFinder();

    private readonly record struct BoundaryDefinition(BaseCoord Coord, TileBoundary Side, TileBoundaryRule Rule);

    [Test, Description("Should return a single coordinate and zero turns when start equals destination")]
    public void ShouldReturnSingleCoordAndZeroTurnsWhenStartEqualsDestination()
    {
        PathMap map = CreateMap(
            [
                [Tile(1m), Tile(1m), Tile(1m)],
                [Tile(1m), Tile(1m), Tile(1m)],
                [Tile(1m), Tile(1m), Tile(1m)]
            ]);

        BaseCoord start = new(1, 1);
        BaseCoord dest = new(1, 1);

        PathResult? result = _pathFinder.FindShortestPath(map, start, dest, 3m);

        Assert.That(result, Is.Not.Null);
        Assert.That(result!.TurnsToDestination, Is.Zero);

        PathStep[] path = [.. result.ShortestPath];
        Assert.That(path, Has.Length.EqualTo(1));
        using (Assert.EnterMultipleScope())
        {
            Assert.That(path[0].Coord, Is.EqualTo(start));
            Assert.That(path[0].ExpectedArrivalTurn, Is.Zero);
        }
    }

    [Test, Description("Should return null when map is null")]
    public void ShouldReturnNullWhenMapIsNull()
    {
        PathResult? result = _pathFinder.FindShortestPath(null!, new BaseCoord(0, 0), new BaseCoord(1, 1), 1m);

        Assert.That(result, Is.Null);
    }

    [Test, Description("Should return null when map is empty")]
    public void ShouldReturnNullWhenMapIsEmpty()
    {
        PathMap map = new()
        {
            Tiles = []
        };

        PathResult? result = _pathFinder.FindShortestPath(map, new BaseCoord(0, 0), new BaseCoord(1, 1), 1m);

        Assert.That(result, Is.Null);
    }

    [Test, Description("Should return null when destination is out of bounds")]
    public void ShouldReturnNullWhenDestinationIsOutOfBounds()
    {
        PathMap map = CreateMap(
            [
                [Tile(1m), Tile(1m)],
                [Tile(1m), Tile(1m)]
            ]);

        PathResult? result = _pathFinder.FindShortestPath(map, new BaseCoord(0, 0), new BaseCoord(2, 2), 1m);

        Assert.That(result, Is.Null);
    }

    [Test, Description("Should return null when start is out of bounds")]
    public void ShouldReturnNullWhenStartIsOutOfBounds()
    {
        PathMap map = CreateMap(
            [
                [Tile(1m), Tile(1m)],
                [Tile(1m), Tile(1m)]
            ]);

        PathResult? result = _pathFinder.FindShortestPath(map, new BaseCoord(-1, 0), new BaseCoord(1, 1), 1m);

        Assert.That(result, Is.Null);
    }

    [Test, Description("Should return null when start tile is impassable")]
    public void ShouldReturnNullWhenStartTileIsImpassable()
    {
        PathMap map = CreateMap(
            [
                [Impassable(), Tile(1m)],
                [Tile(1m), Tile(1m)]
            ]);

        PathResult? result = _pathFinder.FindShortestPath(map, new BaseCoord(0, 0), new BaseCoord(1, 1), 1m);

        Assert.That(result, Is.Null);
    }

    [Test, Description("Should return null when destination tile is impassable")]
    public void ShouldReturnNullWhenDestinationTileIsImpassable()
    {
        PathMap map = CreateMap(
            [
                [Tile(1m), Tile(1m)],
                [Tile(1m), Impassable()]
            ]);

        PathResult? result = _pathFinder.FindShortestPath(map, new BaseCoord(0, 0), new BaseCoord(1, 1), 1m);

        Assert.That(result, Is.Null);
    }

    [Test, Description("Should throw when moves per turn is zero")]
    public void ShouldThrowWhenMovesPerTurnIsZero()
    {
        PathMap map = CreateMap(
            [
                [Tile(1m), Tile(1m)],
                [Tile(1m), Tile(1m)]
            ]);

        Assert.That(
            () => _pathFinder.FindShortestPath(map, new BaseCoord(0, 0), new BaseCoord(1, 1), 0m),
            Throws.TypeOf<ArgumentOutOfRangeException>());
    }

    [Test, Description("Should throw when moves per turn is negative")]
    public void ShouldThrowWhenMovesPerTurnIsNegative()
    {
        PathMap map = CreateMap(
            [
                [Tile(1m), Tile(1m)],
                [Tile(1m), Tile(1m)]
            ]);

        Assert.That(
            () => _pathFinder.FindShortestPath(map, new BaseCoord(0, 0), new BaseCoord(1, 1), -1m),
            Throws.TypeOf<ArgumentOutOfRangeException>());
    }

    [Test, Description("Should throw when map is not rectangular")]
    public void ShouldThrowWhenMapIsNotRectangular()
    {
        PathMap map = new()
        {
            Tiles =
            [
                [Tile(1m), Tile(1m)],
                [Tile(1m)]
            ]
        };

        Assert.That(
            () => _pathFinder.FindShortestPath(map, new BaseCoord(0, 0), new BaseCoord(1, 1), 1m),
            Throws.TypeOf<ArgumentException>());
    }

    [Test, Description("Should find orthogonal path when simple route exists")]
    public void ShouldFindOrthogonalPathWhenSimpleRouteExists()
    {
        PathMap map = CreateMap(
            [
                [Tile(1m), Tile(1m), Tile(1m)]
            ]);

        BaseCoord start = new(0, 0);
        BaseCoord dest = new(2, 0);

        PathResult? result = _pathFinder.FindShortestPath(map, start, dest, 2m);

        Assert.That(result, Is.Not.Null);
        Assert.That(result!.TurnsToDestination, Is.EqualTo(1));

        PathStep[] path = PopAll(result.ShortestPath);

        Assert.That(GetCoords(path), Is.EqualTo(new[]
        {
            new BaseCoord(0, 0),
            new BaseCoord(1, 0),
            new BaseCoord(2, 0)
        }));
    }

    [Test, Description("Should find diagonal path when diagonal is cheapest")]
    public void ShouldFindDiagonalPathWhenDiagonalIsCheapest()
    {
        PathMap map = CreateMap(
            [
                [Tile(1m), Tile(1m)],
                [Tile(1m), Tile(1m)]
            ]);

        BaseCoord start = new(0, 0);
        BaseCoord dest = new(1, 1);

        PathResult? result = _pathFinder.FindShortestPath(map, start, dest, 1m);

        Assert.That(result, Is.Not.Null);
        Assert.That(result!.TurnsToDestination, Is.EqualTo(1));

        PathStep[] path = PopAll(result.ShortestPath);

        Assert.That(GetCoords(path), Is.EqualTo(new[]
        {
            new BaseCoord(0, 0),
            new BaseCoord(1, 1)
        }));
    }

    [Test, Description("Should allow diagonal when only one adjacent side is impassable")]
    public void ShouldAllowDiagonalWhenOnlyOneAdjacentSideIsImpassable()
    {
        PathMap map = CreateMap(
            [
                [Tile(1m), Tile(1m)],
                [Impassable(), Tile(1m)]
            ]);

        BaseCoord start = new(0, 0);
        BaseCoord dest = new(1, 1);

        PathResult? result = _pathFinder.FindShortestPath(map, start, dest, 1m);

        Assert.That(result, Is.Not.Null);

        PathStep[] path = PopAll(result!.ShortestPath);

        Assert.That(GetCoords(path), Is.EqualTo(new[]
        {
            new BaseCoord(0, 0),
            new BaseCoord(1, 1)
        }));
    }

    [Test, Description("Should not allow diagonal when both adjacent sides are impassable")]
    public void ShouldNotAllowDiagonalWhenBothAdjacentSidesAreImpassable()
    {
        PathMap map = CreateMap(
            [
                [Tile(1m), Impassable()],
                [Impassable(), Tile(1m)]
            ]);

        BaseCoord start = new(0, 0);
        BaseCoord dest = new(1, 1);

        PathResult? result = _pathFinder.FindShortestPath(map, start, dest, 1m);

        Assert.That(result, Is.Null);
    }

    [Test, Description("Should not allow orthogonal move when source boundary is blocked")]
    public void ShouldNotAllowOrthogonalMoveWhenSourceBoundaryIsBlocked()
    {
        PathMap map = CreateMap(
            [
                [Tile(1m), Tile(1m)]
            ],
            boundaries:
            [
                .. SharedBoundary(new BaseCoord(0, 0), TileBoundary.East, new BaseCoord(1, 0), TileBoundary.West, Wall())
            ]);

        PathResult? result = _pathFinder.FindShortestPath(map, new BaseCoord(0, 0), new BaseCoord(1, 0), 1m);

        Assert.That(result, Is.Null);
    }

    [Test, Description("Should not allow orthogonal move when destination boundary is blocked")]
    public void ShouldNotAllowOrthogonalMoveWhenDestinationBoundaryIsBlocked()
    {
        PathMap map = CreateMap(
            [
                [Tile(1m), Tile(1m)]
            ],
            boundaries:
            [
                .. SharedBoundary(new BaseCoord(0, 0), TileBoundary.East, new BaseCoord(1, 0), TileBoundary.West, Wall())
            ]);

        PathResult? result = _pathFinder.FindShortestPath(map, new BaseCoord(0, 0), new BaseCoord(1, 0), 1m);

        Assert.That(result, Is.Null);
    }

    [Test, Description("Should choose alternate route when direct boundary is blocked")]
    public void ShouldChooseAlternateRouteWhenDirectBoundaryIsBlocked()
    {
        PathMap map = CreateMap(
            [
                [Tile(1m), Tile(1m), Tile(1m)],
                [Tile(1m), Tile(1m), Tile(1m)]
            ],
            boundaries:
            [
                .. SharedBoundary(new BaseCoord(0, 0), TileBoundary.East, new BaseCoord(1, 0), TileBoundary.West, Wall())
            ]);

        BaseCoord start = new(0, 0);
        BaseCoord dest = new(2, 0);

        PathResult? result = _pathFinder.FindShortestPath(map, start, dest, 10m);

        Assert.That(result, Is.Not.Null);

        PathStep[] path = PopAll(result!.ShortestPath);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(path[0].Coord, Is.EqualTo(start));
            Assert.That(path[^1].Coord, Is.EqualTo(dest));
            Assert.That(path[1].Coord, Is.Not.EqualTo(new BaseCoord(1, 0)));
        }
    }

    [Test, Description("Should not allow diagonal when source tile blocks required boundaries")]
    public void ShouldNotAllowDiagonalWhenSourceTileBlocksRequiredBoundaries()
    {
        PathMap map = CreateMap(
            [
                [Tile(1m), Tile(1m)],
                [Tile(1m), Tile(1m)]
            ],
            boundaries:
            [
                .. SharedBoundary(new BaseCoord(0, 0), TileBoundary.East, new BaseCoord(1, 0), TileBoundary.West, Wall()),
                .. SharedBoundary(new BaseCoord(0, 0), TileBoundary.South, new BaseCoord(0, 1), TileBoundary.North, Wall())
            ]);

        PathResult? result = _pathFinder.FindShortestPath(map, new BaseCoord(0, 0), new BaseCoord(1, 1), 1m);

        Assert.That(result, Is.Null);
    }

    [Test, Description("Should not allow diagonal when destination tile blocks required boundaries")]
    public void ShouldNotAllowDiagonalWhenDestinationTileBlocksRequiredBoundaries()
    {
        PathMap map = CreateMap(
            [
                [Tile(1m), Tile(1m)],
                [Tile(1m), Tile(1m)]
            ],
            boundaries:
            [
                .. SharedBoundary(new BaseCoord(1, 1), TileBoundary.North, new BaseCoord(1, 0), TileBoundary.South, Wall()),
                .. SharedBoundary(new BaseCoord(1, 1), TileBoundary.West, new BaseCoord(0, 1), TileBoundary.East, Wall()),
                .. SharedBoundary(new BaseCoord(0, 0), TileBoundary.East, new BaseCoord(1, 0), TileBoundary.West, Wall()),
                .. SharedBoundary(new BaseCoord(0, 0), TileBoundary.South, new BaseCoord(0, 1), TileBoundary.North, Wall())
            ]);

        PathResult? result = _pathFinder.FindShortestPath(map, new BaseCoord(0, 0), new BaseCoord(1, 1), 1m);

        Assert.That(result, Is.Null);
    }

    [Test, Description("Should return null when destination is completely blocked")]
    public void ShouldReturnNullWhenDestinationIsCompletelyBlocked()
    {
        PathMap map = CreateMap(
            [
                [Tile(1m), Tile(1m), Tile(1m)],
                [Tile(1m), Impassable(), Impassable()],
                [Tile(1m), Impassable(), Tile(1m)]
            ]);

        BaseCoord start = new(0, 0);
        BaseCoord dest = new(2, 2);

        PathResult? result = _pathFinder.FindShortestPath(map, start, dest, 1m);

        Assert.That(result, Is.Null);
    }

    [Test, Description("Should choose lower total cost when shorter geometric path is more expensive")]
    public void ShouldChooseLowerTotalCostWhenShorterGeometricPathIsMoreExpensive()
    {
        PathMap map = CreateMap(
            [
                [Tile(1m), Tile(50m), Tile(1m)],
                [Tile(1m), Tile(50m), Tile(1m)],
                [Tile(1m), Tile(1m), Tile(1m)]
            ]);

        BaseCoord start = new(0, 0);
        BaseCoord dest = new(2, 0);

        PathResult? result = _pathFinder.FindShortestPath(map, start, dest, 10m);

        Assert.That(result, Is.Not.Null);

        PathStep[] path = PopAll(result!.ShortestPath);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(path[0].Coord, Is.EqualTo(start));
            Assert.That(path[^1].Coord, Is.EqualTo(dest));
        }

        decimal totalCost = CalculatePathCost(map, path);
        using (Assert.EnterMultipleScope())
        {
            Assert.That(totalCost, Is.EqualTo(4m));
            Assert.That(result.TurnsToDestination, Is.EqualTo(1));
        }
    }

    [Test, Description("Should calculate turns using ceiling of total cost divided by moves per turn")]
    public void ShouldCalculateTurnsUsingCeilingOfTotalCostDividedByMovesPerTurn()
    {
        PathMap map = CreateMap(
            [
                [Tile(1m), Tile(1m), Tile(1m)]
            ]);

        BaseCoord start = new(0, 0);
        BaseCoord dest = new(2, 0);

        PathResult? result = _pathFinder.FindShortestPath(map, start, dest, 1.5m);

        Assert.That(result, Is.Not.Null);
        Assert.That(result!.TurnsToDestination, Is.EqualTo(2));
    }

    [Test, Description("Should add directional boundary movement cost to the destination tile cost")]
    public void ShouldAddBoundaryMovementCostToOrthogonalStep()
    {
        PathMap map = CreateMap(
            [
                [Tile(1m), Tile(1m)]
            ],
            boundaries:
            [
                .. SharedBoundary(new BaseCoord(0, 0), TileBoundary.East, new BaseCoord(1, 0), TileBoundary.West, River(2m))
            ]);

        PathResult? result = _pathFinder.FindShortestPath(map, new BaseCoord(0, 0), new BaseCoord(1, 0), 2m);

        Assert.That(result, Is.Not.Null);

        PathStep[] path = PopAll(result!.ShortestPath);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(result.TurnsToDestination, Is.EqualTo(2));
            Assert.That(GetExpectedArrivalTurns(path), Is.EqualTo(new int?[] { 0, 2 }));
            Assert.That(CalculatePathCost(map, path), Is.EqualTo(3m));
        }
    }

    [Test, Description("Should use the most restrictive boundary cost for diagonal movement")]
    public void ShouldUseMostRestrictiveBoundaryCostForDiagonalMovement()
    {
        PathMap map = CreateMap(
            [
                [Tile(1m), Tile(10m)],
                [Tile(10m), Tile(1m)]
            ],
            boundaries:
            [
                .. SharedBoundary(new BaseCoord(0, 0), TileBoundary.East, new BaseCoord(1, 0), TileBoundary.West, River(3m)),
                .. SharedBoundary(new BaseCoord(0, 0), TileBoundary.South, new BaseCoord(0, 1), TileBoundary.North, River(1m))
            ]);

        PathResult? result = _pathFinder.FindShortestPath(map, new BaseCoord(0, 0), new BaseCoord(1, 1), 2m);

        Assert.That(result, Is.Not.Null);

        PathStep[] path = PopAll(result!.ShortestPath);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(GetCoords(path), Is.EqualTo(new[] { new BaseCoord(0, 0), new BaseCoord(1, 1) }));
            Assert.That(result.TurnsToDestination, Is.EqualTo(2));
            Assert.That(CalculatePathCost(map, path), Is.EqualTo(4m));
        }
    }

    [Test, Description("Should mark each turn end when one tile can be moved per turn")]
    public void ShouldMarkEachTurnEndWhenOneTileCanBeMovedPerTurn()
    {
        PathMap map = CreateMap(
            [
                [Tile(1m), Tile(1m), Tile(1m), Tile(1m)]
            ]);

        PathResult? result = _pathFinder.FindShortestPath(map, new BaseCoord(0, 0), new BaseCoord(3, 0), 1m);

        Assert.That(result, Is.Not.Null);

        PathStep[] path = PopAll(result!.ShortestPath);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(GetCoords(path), Is.EqualTo(new[]
                {
            new BaseCoord(0, 0),
            new BaseCoord(1, 0),
            new BaseCoord(2, 0),
            new BaseCoord(3, 0)
        }));

            Assert.That(GetExpectedArrivalTurns(path), Is.EqualTo(new int?[] { 0, 1, 2, 3 }));
        }
    }

    [Test, Description("Should only mark turn end tiles when two tiles can be moved per turn")]
    public void ShouldOnlyMarkTurnEndTilesWhenTwoTilesCanBeMovedPerTurn()
    {
        PathMap map = CreateMap(
            [
                [Tile(1m), Tile(1m), Tile(1m), Tile(1m), Tile(1m)]
            ]);

        PathResult? result = _pathFinder.FindShortestPath(map, new BaseCoord(0, 0), new BaseCoord(4, 0), 2m);

        Assert.That(result, Is.Not.Null);

        PathStep[] path = PopAll(result!.ShortestPath);

        Assert.That(GetExpectedArrivalTurns(path), Is.EqualTo(new int?[] { 0, null, 1, null, 2 }));
    }

    [Test, Description("Should return path with start as first and destination as last when popping stack")]
    public void ShouldReturnPathWithStartAsFirstAndDestinationAsLastWhenPoppingStack()
    {
        PathMap map = CreateMap(
            [
                [Tile(1m), Tile(1m), Tile(1m)],
                [Tile(1m), Tile(1m), Tile(1m)],
                [Tile(1m), Tile(1m), Tile(1m)]
            ]);

        BaseCoord start = new(0, 0);
        BaseCoord dest = new(2, 2);

        PathResult? result = _pathFinder.FindShortestPath(map, start, dest, 10m);

        Assert.That(result, Is.Not.Null);

        PathStep[] path = PopAll(result!.ShortestPath);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(path[0].Coord, Is.EqualTo(start));
            Assert.That(path[^1].Coord, Is.EqualTo(dest));
        }
    }

    [Test, Description("Should prefer single diagonal step when that is lowest cost path")]
    public void ShouldPreferSingleDiagonalStepWhenThatIsLowestCostPath()
    {
        PathMap map = CreateMap(
            [
                [Tile(1m), Tile(1m), Tile(1m)],
                [Tile(1m), Tile(1m), Tile(1m)],
                [Tile(1m), Tile(1m), Tile(1m)]
            ]);

        BaseCoord start = new(0, 0);
        BaseCoord dest = new(1, 1);

        PathResult? result = _pathFinder.FindShortestPath(map, start, dest, 10m);

        Assert.That(result, Is.Not.Null);

        PathStep[] path = PopAll(result!.ShortestPath);

        Assert.That(GetCoords(path), Is.EqualTo(new[]
        {
            new BaseCoord(0, 0),
            new BaseCoord(1, 1)
        }));
    }

    [Test, Description("Should use horizontal wrapping when enabled")]
    public void ShouldUseHorizontalWrappingWhenEnabled()
    {
        PathMap map = CreateMap(
            [
                [Tile(1m), Tile(10m), Tile(1m)]
            ],
            hasHorizontalWrapping: true);

        PathResult? result = _pathFinder.FindShortestPath(map, new BaseCoord(0, 0), new BaseCoord(2, 0), 1m);

        Assert.That(result, Is.Not.Null);

        PathStep[] path = PopAll(result!.ShortestPath);

        Assert.That(GetCoords(path), Is.EqualTo(new[]
        {
            new BaseCoord(0, 0),
            new BaseCoord(2, 0)
        }));
    }

    [Test, Description("Should use vertical wrapping when enabled")]
    public void ShouldUseVerticalWrappingWhenEnabled()
    {
        PathMap map = CreateMap(
            [
                [Tile(1m)],
                [Tile(10m)],
                [Tile(1m)]
            ],
            hasVerticalWrapping: true);

        PathResult? result = _pathFinder.FindShortestPath(map, new BaseCoord(0, 0), new BaseCoord(0, 2), 1m);

        Assert.That(result, Is.Not.Null);

        PathStep[] path = PopAll(result!.ShortestPath);

        Assert.That(GetCoords(path), Is.EqualTo(new[]
        {
            new BaseCoord(0, 0),
            new BaseCoord(0, 2)
        }));
    }

    [Test, Description("Should respect blocked boundary across horizontal wrap")]
    public void ShouldRespectBlockedBoundaryAcrossHorizontalWrap()
    {
        PathMap map = CreateMap(
            [
                [Tile(1m), Tile(10m), Tile(1m)]
            ],
            hasHorizontalWrapping: true,
            boundaries:
            [
                .. SharedBoundary(new BaseCoord(0, 0), TileBoundary.West, new BaseCoord(2, 0), TileBoundary.East, Wall())
            ]);

        PathResult? result = _pathFinder.FindShortestPath(map, new BaseCoord(0, 0), new BaseCoord(2, 0), 20m);

        Assert.That(result, Is.Not.Null);

        PathStep[] path = PopAll(result!.ShortestPath);

        Assert.That(GetCoords(path), Is.EqualTo(new[]
        {
            new BaseCoord(0, 0),
            new BaseCoord(1, 0),
            new BaseCoord(2, 0)
        }));
    }

    [Test, Description("Should handle larger map with mixed terrain")]
    public void ShouldHandleLargerMapWithMixedTerrain()
    {
        PathMap map = CreateMap(
            [
                [Tile(1m), Tile(1m), Tile(1m), Tile(1m), Tile(1m)],
                [Tile(1m), Impassable(), Impassable(), Impassable(), Tile(1m)],
                [Tile(1m), Tile(1m), Tile(5m), Tile(1m), Tile(1m)],
                [Tile(1m), Impassable(), Tile(1m), Tile(1m), Tile(1m)],
                [Tile(1m), Tile(1m), Tile(1m), Impassable(), Tile(1m)]
            ]);

        BaseCoord start = new(0, 0);
        BaseCoord dest = new(4, 4);

        PathResult? result = _pathFinder.FindShortestPath(map, start, dest, 3m);

        Assert.That(result, Is.Not.Null);

        PathStep[] path = PopAll(result!.ShortestPath);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(path[0].Coord, Is.EqualTo(start));
            Assert.That(path[^1].Coord, Is.EqualTo(dest));
            Assert.That(result.TurnsToDestination, Is.GreaterThan(0));
        }
    }

    private static PathMap CreateMap(
        PathTile[][] rowsByY,
        bool hasHorizontalWrapping = false,
        bool hasVerticalWrapping = false,
        IReadOnlyList<BoundaryDefinition>? boundaries = null)
    {
        if (rowsByY.Length == 0)
        {
            return new PathMap
            {
                Tiles = Array.Empty<PathTile[]>(),
                HasHorizontalWrapping = hasHorizontalWrapping,
                HasVerticalWrapping = hasVerticalWrapping,
                Boundaries = new Dictionary<TileBoundaryKey, TileBoundaryRule>()
            };
        }

        int height = rowsByY.Length;
        int width = rowsByY[0].Length;

        PathTile[][] tiles = new PathTile[width][];
        for (int x = 0; x < width; x++)
        {
            tiles[x] = new PathTile[height];
        }

        for (int y = 0; y < height; y++)
        {
            if (rowsByY[y].Length != width)
            {
                throw new ArgumentException("Input rows must all be the same length.", nameof(rowsByY));
            }

            for (int x = 0; x < width; x++)
            {
                tiles[x][y] = rowsByY[y][x];
            }
        }

        return new PathMap
        {
            Tiles = tiles,
            HasHorizontalWrapping = hasHorizontalWrapping,
            HasVerticalWrapping = hasVerticalWrapping,
            Boundaries = (boundaries ?? []).ToDictionary(
                boundary => new TileBoundaryKey(boundary.Coord, boundary.Side),
                boundary => boundary.Rule)
        };
    }

    private static PathTile Tile(decimal cost)
    {
        return new PathTile(cost);
    }

    private static PathTile Impassable()
    {
        return new PathTile(0m, true);
    }

    private static TileBoundaryRule Wall()
    {
        return new TileBoundaryRule(TileBoundaryType.Blocked);
    }

    private static TileBoundaryRule River(decimal additionalMovementCost)
    {
        return new TileBoundaryRule(TileBoundaryType.TerrainFeature, additionalMovementCost);
    }

    private static BoundaryDefinition[] SharedBoundary(
        BaseCoord firstCoord,
        TileBoundary firstSide,
        BaseCoord secondCoord,
        TileBoundary secondSide,
        TileBoundaryRule rule)
    {
        return
        [
            new BoundaryDefinition(firstCoord, firstSide, rule),
            new BoundaryDefinition(secondCoord, secondSide, rule)
        ];
    }

    private static PathStep[] PopAll(Stack<PathStep> stack)
    {
        List<PathStep> steps = new();

        while (stack.Count > 0)
        {
            steps.Add(stack.Pop());
        }

        return [.. steps];
    }

    private static BaseCoord[] GetCoords(IReadOnlyList<PathStep> path)
    {
        return [.. path.Select(step => step.Coord)];
    }

    private static int?[] GetExpectedArrivalTurns(IReadOnlyList<PathStep> path)
    {
        return [.. path.Select(step => step.ExpectedArrivalTurn)];
    }

    private static decimal CalculatePathCost(PathMap map, IReadOnlyList<PathStep> path)
    {
        decimal total = 0m;

        for (int i = 1; i < path.Count; i++)
        {
            BaseCoord previous = path[i - 1].Coord;
            BaseCoord step = path[i].Coord;

            total += map.Tiles[step.X][step.Y].Cost + GetBoundaryMovementCost(map, previous, step);
        }

        return total;
    }

    private static decimal GetBoundaryMovementCost(PathMap map, BaseCoord current, BaseCoord next)
    {
        (int deltaX, int deltaY) = ResolveDelta(map, current, next);

        if (Math.Abs(deltaX) == 1 && Math.Abs(deltaY) == 1)
        {
            decimal horizontalCost = GetOrthogonalBoundaryCost(map, current, deltaX, 0);
            decimal verticalCost = GetOrthogonalBoundaryCost(map, current, 0, deltaY);

            return Math.Max(horizontalCost, verticalCost);
        }

        return GetOrthogonalBoundaryCost(map, current, deltaX, deltaY);
    }

    private static decimal GetOrthogonalBoundaryCost(PathMap map, BaseCoord current, int deltaX, int deltaY)
    {
        TileBoundary currentBoundary = GetBoundary(deltaX, deltaY);
        BaseCoord next = ResolveNeighbor(map, current, deltaX, deltaY);
        TileBoundary oppositeBoundary = GetOppositeBoundary(currentBoundary);

        bool hasCurrentRule = map.Boundaries.TryGetValue(new TileBoundaryKey(current, currentBoundary), out TileBoundaryRule currentRule);
        bool hasNextRule = map.Boundaries.TryGetValue(new TileBoundaryKey(next, oppositeBoundary), out TileBoundaryRule nextRule);

        if (hasCurrentRule && hasNextRule && currentRule != nextRule)
        {
            throw new InvalidOperationException("Test boundary definitions are inconsistent across a shared edge.");
        }

        TileBoundaryRule rule = hasCurrentRule ? currentRule : hasNextRule ? nextRule : default;
        return rule.IsBlocked ? 0m : rule.AdditionalMovementCost;
    }

    private static BaseCoord ResolveNeighbor(PathMap map, BaseCoord current, int deltaX, int deltaY)
    {
        int x = current.X + deltaX;
        int y = current.Y + deltaY;

        if (map.HasHorizontalWrapping)
        {
            x = ((x % map.Tiles.Length) + map.Tiles.Length) % map.Tiles.Length;
        }

        if (map.HasVerticalWrapping)
        {
            y = ((y % map.Tiles[0].Length) + map.Tiles[0].Length) % map.Tiles[0].Length;
        }

        return new BaseCoord(x, y);
    }

    private static (int DeltaX, int DeltaY) ResolveDelta(PathMap map, BaseCoord current, BaseCoord next)
    {
        int width = map.Tiles.Length;
        int height = map.Tiles[0].Length;
        int deltaX = next.X - current.X;
        int deltaY = next.Y - current.Y;

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

        return (deltaX, deltaY);
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
}
