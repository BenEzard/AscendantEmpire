using AscendantEmpire.Application.Features.ForceOrders.Movement;
using AscendantEmpire.Application.Features.ForceOrders.Movement.Strategies;
using AscendantEmpire.BackendContracts.CampaignMaps;
using AscendantEmpire.Domain.Enums.Maps;

namespace AscendantEmpire.Tests.Features.ForceOrders.Movement;

/// <summary>
/// Pure unit tests for the river rules: <see cref="RiverEdges"/>, <see cref="RiverMovementRules"/> and the costs and sailing rules built on them. No
/// database, because everything here works from a snapshot of river columns.
/// </summary>
/// <remarks>
/// <para>
/// Most tests use a 3 x 3 map that does not wrap, with the Force standing in the middle at (1, 1). Rivers are placed the way they are stored: each tile
/// owns only its South and East edges, so a river on a tile's North edge is placed as the South edge of the tile above it.
/// </para>
/// </remarks>
[TestFixture]
internal class RiverMovementRulesTests
{
    /// <summary>The middle of the 3 x 3 test map, where the Force stands unless a test says otherwise.</summary>
    private static readonly Coord _ORIGIN = new(1, 1);

    /// <summary>
    /// Builds the river columns of a small map one edge at a time, in the canonical South/East form they are stored in.
    /// </summary>
    private sealed class RiverMapBuilder(int width = 3, int height = 3)
    {
        private readonly Dictionary<Coord, TileRiverEdges> _tiles = CreateEmptyTiles(width, height);

        private static Dictionary<Coord, TileRiverEdges> CreateEmptyTiles(int width, int height)
        {
            Dictionary<Coord, TileRiverEdges> tiles = [];

            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    tiles[new Coord(x, y)] = new TileRiverEdges(RiverWidth.None, RiverWidth.None);
                }
            }

            return tiles;
        }

        /// <summary>Puts a river on the East edge of (x, y): the edge it shares with (x + 1, y).</summary>
        public RiverMapBuilder East(int x, int y, RiverWidth width)
        {
            Coord coord = new(x, y);
            _tiles[coord] = _tiles[coord] with { East = width };
            return this;
        }

        /// <summary>Puts a river on the South edge of (x, y): the edge it shares with (x, y + 1).</summary>
        public RiverMapBuilder South(int x, int y, RiverWidth width)
        {
            Coord coord = new(x, y);
            _tiles[coord] = _tiles[coord] with { South = width };
            return this;
        }

        /// <summary>The snapshot a step from <paramref name="origin"/> would see.</summary>
        public RiverEdges Around(Coord origin, bool wrapsHorizontally = false, bool wrapsVertically = false)
        {
            return RiverEdges.Create(origin, _tiles, width, height, wrapsHorizontally, wrapsVertically);
        }
    }

    /// <summary>A target tile of the given cost, land or water.</summary>
    private static MovementTargetTile Tile(decimal movementCost = 1m, bool isWater = false)
    {
        return new MovementTargetTile
        {
            XCoord = 2,
            YCoord = 1,
            GeographyType = isWater ? CampaignMapGeographyType.DeepWater : CampaignMapGeographyType.Flat,
            GeographyName = isWater ? "Deep Water" : "Flat",
            GeographyMovementCost = movementCost,
            BiomeName = isWater ? "Ocean" : "Grassland",
            BiomeMovementCost = 1m,
            BiomeIsWater = isWater,
            GeographyIsWater = isWater,
            BiomeIsTraversable = true,
            GeographyIsTraversable = true
        };
    }

    /// <summary>A step onto a tile of the given cost, with the given rivers around it.</summary>
    private static MovementStep Step(
        RiverStepEffect rivers,
        decimal movementCost = 1m,
        bool targetIsWater = false,
        bool originIsWater = false,
        bool isDiagonal = false)
    {
        return new MovementStep
        {
            Target = Tile(movementCost, targetIsWater),
            OriginIsWater = originIsWater,
            IsDiagonal = isDiagonal,
            Rivers = rivers
        };
    }

    // ---------------------------------------------------------------------------------------------------------------------------------------------
    // Crossing
    // ---------------------------------------------------------------------------------------------------------------------------------------------

    [Test, Description("With no river anywhere near it, a step crosses nothing and follows nothing, so the river rules leave it exactly as it was.")]
    public void ShouldFindNoRiverWhereThereIsNone()
    {
        RiverStepEffect effect = RiverMovementRules.Evaluate(new RiverMapBuilder().Around(_ORIGIN), MoveDirection.East);

        Assert.That(effect, Is.EqualTo(RiverStepEffect.None));
    }

    [Test, Description("Each orthogonal step crosses the edge the two tiles share, whichever of them owns it in storage.")]
    [TestCase(MoveDirection.East, Description = "East: the origin owns the edge as its East.")]
    [TestCase(MoveDirection.West, Description = "West: the tile to the west owns the edge as its East.")]
    [TestCase(MoveDirection.South, Description = "South: the origin owns the edge as its South.")]
    [TestCase(MoveDirection.North, Description = "North: the tile above owns the edge as its South.")]
    public void ShouldCrossTheSharedEdgeOnAnOrthogonalStep(MoveDirection direction)
    {
        RiverEdges edges = new RiverMapBuilder()
            .East(1, 1, RiverWidth.Medium)
            .East(0, 1, RiverWidth.Medium)
            .South(1, 1, RiverWidth.Medium)
            .South(1, 0, RiverWidth.Medium)
            .Around(_ORIGIN);

        RiverStepEffect effect = RiverMovementRules.Evaluate(edges, direction);

        Assert.That(effect.Crossed, Is.EqualTo(RiverWidth.Medium));
    }

    [Test, Description("The crossed width is the edge's own width, so a Large river is reported as Large and the Land rule can refuse it.")]
    public void ShouldReportTheWidthOfTheRiverCrossed()
    {
        RiverEdges edges = new RiverMapBuilder().East(1, 1, RiverWidth.Large).Around(_ORIGIN);

        Assert.That(RiverMovementRules.Evaluate(edges, MoveDirection.East).Crossed, Is.EqualTo(RiverWidth.Large));
    }

    [Test, Description("A straight river between two columns separates the tiles on either side of it, so a diagonal step across it is a crossing.")]
    public void ShouldCrossAStraightRiverDiagonally()
    {
        // A river running north-south between columns 1 and 2, down both rows the diagonal passes through.
        RiverEdges edges = new RiverMapBuilder().East(1, 1, RiverWidth.Medium).East(1, 2, RiverWidth.Medium).Around(_ORIGIN);

        Assert.That(RiverMovementRules.Evaluate(edges, MoveDirection.SouthEast).Crossed, Is.EqualTo(RiverWidth.Medium));
    }

    [Test, Description("A river that ends at the corner a diagonal passes through does not separate the two tiles, so the step can go round its end and crosses nothing.")]
    public void ShouldNotCrossARiverThatEndsAtTheCorner()
    {
        // The river comes down between columns 1 and 2 and stops at the corner below (1, 1).
        RiverEdges edges = new RiverMapBuilder().East(1, 1, RiverWidth.Large).Around(_ORIGIN);

        Assert.That(RiverMovementRules.Evaluate(edges, MoveDirection.SouthEast).Crossed, Is.EqualTo(RiverWidth.None));
    }

    [Test, Description("A river that bends round the target tile's far side leaves origin and target on the same bank, so the diagonal crosses nothing.")]
    public void ShouldNotCrossARiverThatBendsAwayFromTheStep()
    {
        // Down between (1, 1) and (2, 1), then east along the bottom of (2, 1): it wraps the corner tile, not the step.
        RiverEdges edges = new RiverMapBuilder().East(1, 1, RiverWidth.Large).South(2, 1, RiverWidth.Large).Around(_ORIGIN);

        Assert.That(RiverMovementRules.Evaluate(edges, MoveDirection.SouthEast).Crossed, Is.EqualTo(RiverWidth.None));
    }

    [Test, Description("A river that bends through the corner between origin and target separates them, so the diagonal crosses it.")]
    public void ShouldCrossARiverThatBendsThroughTheCorner()
    {
        // Down between (1, 1) and (2, 1), then west along the bottom of (1, 1): origin and target now lie on opposite banks.
        RiverEdges edges = new RiverMapBuilder().East(1, 1, RiverWidth.Large).South(1, 1, RiverWidth.Large).Around(_ORIGIN);

        Assert.That(RiverMovementRules.Evaluate(edges, MoveDirection.SouthEast).Crossed, Is.EqualTo(RiverWidth.Large));
    }

    [Test, Description("When both ways round the corner cross a river, the Force takes the easier way, so the narrower of the two routes is what it crosses.")]
    public void ShouldCrossTheEasierWayRoundTheCorner()
    {
        // A Large river down the east side of the origin and a Medium one along its bottom, meeting at the corner the diagonal passes through.
        RiverEdges edges = new RiverMapBuilder().East(1, 1, RiverWidth.Large).South(1, 1, RiverWidth.Medium).Around(_ORIGIN);

        Assert.That(RiverMovementRules.Evaluate(edges, MoveDirection.SouthEast).Crossed, Is.EqualTo(RiverWidth.Medium));
    }

    // ---------------------------------------------------------------------------------------------------------------------------------------------
    // Along
    // ---------------------------------------------------------------------------------------------------------------------------------------------

    [Test, Description("An east step with a river along the bottom of both tiles runs along it without crossing it.")]
    public void ShouldRunAlongARiverOnTheSameBank()
    {
        RiverEdges edges = new RiverMapBuilder().South(1, 1, RiverWidth.Large).South(2, 1, RiverWidth.Large).Around(_ORIGIN);

        RiverStepEffect effect = RiverMovementRules.Evaluate(edges, MoveDirection.East);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(effect.Along, Is.EqualTo(RiverWidth.Large));
            Assert.That(effect.Crossed, Is.EqualTo(RiverWidth.None));
        }
    }

    [Test, Description("A north step runs along a river on the west edges of both tiles, which are stored as the East edges of the column beside them.")]
    public void ShouldRunAlongARiverOnTheWestSideOfANorthStep()
    {
        RiverEdges edges = new RiverMapBuilder().East(0, 1, RiverWidth.Medium).East(0, 0, RiverWidth.Medium).Around(_ORIGIN);

        Assert.That(RiverMovementRules.Evaluate(edges, MoveDirection.North).Along, Is.EqualTo(RiverWidth.Medium));
    }

    [Test, Description("A river beside only one of the two tiles ends or turns between them, so there is no run to follow.")]
    public void ShouldNotRunAlongARiverBesideOnlyOneTile()
    {
        RiverEdges edges = new RiverMapBuilder().South(1, 1, RiverWidth.Large).Around(_ORIGIN);

        Assert.That(RiverMovementRules.Evaluate(edges, MoveDirection.East).Along, Is.EqualTo(RiverWidth.None));
    }

    [Test, Description("A run of river is only as wide as its narrower half, so Medium beside one tile and Large beside the next is followed as Medium.")]
    public void ShouldFollowTheNarrowerHalfOfARun()
    {
        RiverEdges edges = new RiverMapBuilder().South(1, 0, RiverWidth.Medium).South(2, 0, RiverWidth.Large).Around(_ORIGIN);

        Assert.That(RiverMovementRules.Evaluate(edges, MoveDirection.East).Along, Is.EqualTo(RiverWidth.Medium));
    }

    [Test, Description("With a river along both sides of the step, the wider one is the one followed.")]
    public void ShouldFollowTheWiderSideWhenBothQualify()
    {
        RiverEdges edges = new RiverMapBuilder()
            .South(1, 0, RiverWidth.Medium)
            .South(2, 0, RiverWidth.Medium)
            .South(1, 1, RiverWidth.Large)
            .South(2, 1, RiverWidth.Large)
            .Around(_ORIGIN);

        Assert.That(RiverMovementRules.Evaluate(edges, MoveDirection.East).Along, Is.EqualTo(RiverWidth.Large));
    }

    [Test, Description("A diagonal step never runs along a river, even one it cuts the corner of, because only a straight step keeps to one bank.")]
    public void ShouldNeverRunAlongARiverDiagonally()
    {
        RiverEdges edges = new RiverMapBuilder().South(0, 1, RiverWidth.Large).South(1, 1, RiverWidth.Large).South(2, 1, RiverWidth.Large).Around(_ORIGIN);

        RiverStepEffect effect = RiverMovementRules.Evaluate(edges, MoveDirection.NorthEast);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(effect.Along, Is.EqualTo(RiverWidth.None));
            Assert.That(effect.TargetBank, Is.EqualTo(RiverWidth.None));
        }
    }

    [Test, Description("A step can both cross a tributary and run along a main river at once, and both are reported.")]
    public void ShouldReportACrossingAndARunTogether()
    {
        RiverEdges edges = new RiverMapBuilder()
            .East(1, 1, RiverWidth.Medium)
            .South(1, 1, RiverWidth.Large)
            .South(2, 1, RiverWidth.Large)
            .Around(_ORIGIN);

        RiverStepEffect effect = RiverMovementRules.Evaluate(edges, MoveDirection.East);

        Assert.That(effect, Is.EqualTo(new RiverStepEffect(RiverWidth.Medium, RiverWidth.Large, RiverWidth.Large)));
    }

    [Test, Description("The target bank is the widest river on the target tile's sides parallel to the step, which is what a ship entering a river mouth needs to see.")]
    public void ShouldReportTheRiverOnTheTargetsParallelSides()
    {
        // A step south onto (1, 2), whose east edge carries a Large river; the origin has none, so this is a mouth rather than a run.
        RiverEdges edges = new RiverMapBuilder().East(1, 2, RiverWidth.Large).Around(_ORIGIN);

        RiverStepEffect effect = RiverMovementRules.Evaluate(edges, MoveDirection.South);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(effect.TargetBank, Is.EqualTo(RiverWidth.Large));
            Assert.That(effect.Along, Is.EqualTo(RiverWidth.None));
        }
    }

    // ---------------------------------------------------------------------------------------------------------------------------------------------
    // Wrapping and the map's edge
    // ---------------------------------------------------------------------------------------------------------------------------------------------

    [Test, Description("On a map that wraps, the last column's East edge is the edge it shares with column 0, so stepping west off column 0 crosses it.")]
    public void ShouldCrossTheWrappedEdgeOnAWrappingMap()
    {
        RiverEdges edges = new RiverMapBuilder().East(2, 1, RiverWidth.Medium).Around(new Coord(0, 1), wrapsHorizontally: true);

        Assert.That(RiverMovementRules.Evaluate(edges, MoveDirection.West).Crossed, Is.EqualTo(RiverWidth.Medium));
    }

    [Test, Description("On a map that does not wrap, the last column's East edge faces off the map, so a stray value there reads as no river at all.")]
    public void ShouldReadAnEdgeFacingOffTheMapAsNoRiver()
    {
        RiverEdges edges = new RiverMapBuilder().East(2, 1, RiverWidth.Large).Around(new Coord(2, 1));

        Assert.That(edges.GetEdge(0, 0, 1, 0), Is.EqualTo(RiverWidth.None));
    }

    [Test, Description("Asking for an edge between cells that are not orthogonal neighbours is a programming error, not a question with an answer.")]
    public void ShouldRefuseAnEdgeBetweenCellsThatAreNotNeighbours()
    {
        RiverEdges edges = new RiverMapBuilder().Around(_ORIGIN);

        Assert.That(() => edges.GetEdge(0, 0, 1, 1), Throws.TypeOf<ArgumentOutOfRangeException>());
    }

    // ---------------------------------------------------------------------------------------------------------------------------------------------
    // Land costs
    // ---------------------------------------------------------------------------------------------------------------------------------------------

    [Test, Description("A Land step pays the tile's cost, plus the crossing cost for a Medium river, less the reduction for a river it runs along.")]
    [TestCase(RiverWidth.None, RiverWidth.None, 1.0, Description = "No river: the tile's cost is the whole price.")]
    [TestCase(RiverWidth.Medium, RiverWidth.None, 2.0, Description = "Crossing a Medium river adds a point.")]
    [TestCase(RiverWidth.None, RiverWidth.Medium, 0.75, Description = "Running along a Medium river takes off a quarter.")]
    [TestCase(RiverWidth.None, RiverWidth.Large, 0.5, Description = "Running along a Large river takes off a half.")]
    [TestCase(RiverWidth.Medium, RiverWidth.Large, 1.5, Description = "Crossing a tributary beside a main river: both apply.")]
    public void ShouldPriceALandStepByTheRiversItCrossesAndFollows(RiverWidth crossed, RiverWidth along, decimal expectedCost)
    {
        MovementStep step = Step(new RiverStepEffect(crossed, along, RiverWidth.None));

        Assert.That(RiverMovementRules.GetLandStepCost(step), Is.EqualTo(expectedCost));
    }

    [Test, Description("A river can make a step cheaper but never free, so the cost is floored at the minimum however cheap the ground.")]
    public void ShouldNeverPriceALandStepBelowTheMinimum()
    {
        // A hypothetical half-point tile along a Large river would otherwise come to nothing at all.
        MovementStep step = Step(new RiverStepEffect(RiverWidth.None, RiverWidth.Large, RiverWidth.Large), movementCost: 0.5m);

        Assert.That(RiverMovementRules.GetLandStepCost(step), Is.EqualTo(RiverMovementRules.MINIMUM_STEP_COST));
    }

    // ---------------------------------------------------------------------------------------------------------------------------------------------
    // Sailing
    // ---------------------------------------------------------------------------------------------------------------------------------------------

    [Test, Description("Open sea to open sea is the ordinary case for a ship, whatever the rivers are doing.")]
    public void ShouldLetAShipSailOpenWater()
    {
        MovementStep step = Step(RiverStepEffect.None, targetIsWater: true, originIsWater: true, isDiagonal: true);

        Assert.That(RiverMovementRules.GetSailingRefusal(step), Is.Null);
    }

    [Test, Description("A ship may leave the sea for land only at the mouth of a Large river, and only straight in.")]
    [TestCase(RiverWidth.Large, false, null, Description = "The mouth of a Large river, entered straight on.")]
    [TestCase(RiverWidth.Medium, false, "Cannot enter land.", Description = "A Medium river is too narrow to sail up.")]
    [TestCase(RiverWidth.None, false, "Cannot enter land.", Description = "An ordinary coast is a landing, not a move.")]
    [TestCase(RiverWidth.Large, true, "Cannot enter land.", Description = "No diagonal step involves land.")]
    public void ShouldLetAShipIntoARiverMouthOnly(RiverWidth targetBank, bool isDiagonal, string? expectedRefusal)
    {
        MovementStep step = Step(new RiverStepEffect(RiverWidth.None, RiverWidth.None, targetBank), originIsWater: true, isDiagonal: isDiagonal);

        Assert.That(RiverMovementRules.GetSailingRefusal(step), Is.EqualTo(expectedRefusal));
    }

    [Test, Description("Once on a river, a ship may carry on only along a continuous Large river, and only straight along it.")]
    [TestCase(RiverWidth.Large, false, null, Description = "Straight up a Large river.")]
    [TestCase(RiverWidth.Medium, false, "Can only sail along a large river.", Description = "A Medium run is too narrow.")]
    [TestCase(RiverWidth.None, false, "Can only sail along a large river.", Description = "Striking out overland.")]
    [TestCase(RiverWidth.Large, true, "Can only sail along a large river.", Description = "Cutting a corner of the river.")]
    public void ShouldLetAShipCarryOnOnlyAlongALargeRiver(RiverWidth along, bool isDiagonal, string? expectedRefusal)
    {
        MovementStep step = Step(new RiverStepEffect(RiverWidth.None, along, RiverWidth.None), isDiagonal: isDiagonal);

        Assert.That(RiverMovementRules.GetSailingRefusal(step), Is.EqualTo(expectedRefusal));
    }

    [Test, Description("A ship can always put straight back out to sea from a river, so it is never stranded on land, but not round a corner.")]
    [TestCase(false, null, Description = "Straight out onto the water.")]
    [TestCase(true, "Cannot sail diagonally off a river.", Description = "Diagonally off the river.")]
    public void ShouldLetAShipPutBackOutToSea(bool isDiagonal, string? expectedRefusal)
    {
        MovementStep step = Step(RiverStepEffect.None, targetIsWater: true, isDiagonal: isDiagonal);

        Assert.That(RiverMovementRules.GetSailingRefusal(step), Is.EqualTo(expectedRefusal));
    }

    [Test, Description("A ship pays the tile's cost on water and the flat sailing cost on a river, whatever the land beside the river is like.")]
    public void ShouldPriceASailingStep()
    {
        MovementStep ontoWater = Step(RiverStepEffect.None, movementCost: 1m, targetIsWater: true);
        MovementStep ontoRiver = Step(new RiverStepEffect(RiverWidth.None, RiverWidth.Large, RiverWidth.Large), movementCost: 4m);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(RiverMovementRules.GetSailingStepCost(ontoWater), Is.EqualTo(1m));
            Assert.That(RiverMovementRules.GetSailingStepCost(ontoRiver), Is.EqualTo(RiverMovementRules.RIVER_SAILING_COST), "Hilly jungle banks don't slow a boat.");
        }
    }
}
