using AscendantEmpire.Application.Features.Maps.CampaignMaps.Classes;
using AscendantEmpire.BackendContracts.CampaignMaps;
using AscendantEmpire.Domain.Enums.Maps;

namespace AscendantEmpire.Tests.Features.Maps.CampaignMaps;

/// <summary>
/// Pure unit tests for <see cref="TerritoryEnclosure"/> — no database, because the rule reads only the tile snapshots it is handed.
/// </summary>
/// <remarks>
/// <para>
/// These pin the Enclosure section of <c>docs/design/campaign-map/tile-ownership.md</c>: a complete ring absorbs, a rival anywhere on the boundary refuses, the interior
/// takes the weakest tier in the ring, and the two kinds of seal - a Medium-or-wider river edge, and impassable terrain - behave differently from one another.
/// </para>
/// <para>
/// The map is 9 x 9, unclaimed Flat Grassland unless a test paints it, and does not wrap unless a test says so.
/// </para>
/// </remarks>
[TestFixture]
internal class TerritoryEnclosureTests
{
    private const int _PLAYER = 1;
    private const int _RIVAL = 2;

    /// <summary>
    /// A grid with a rival holding one far corner, which is what every real Game looks like.
    /// </summary>
    /// <remarks>
    /// The corner tile matters, and the design says why: "a band of territory around a wrapping world would absorb the rest of it, except that the rest of it holds a
    /// rival, whose tiles on the boundary block the absorption. A game has at least two Players, so that always applies." The same is true of a non-wrapping map, whose
    /// edge seals exactly as the Void does - so without a rival somewhere, one Player's few tiles would ring the <i>whole</i> remaining map and absorb it. A fixture with
    /// no rival would therefore pass or fail for reasons that have nothing to do with the pocket under test.
    /// </remarks>
    private static TerritoryTestGrid NewGrid(bool wrapsHorizontally = false, bool wrapsVertically = false, bool withRival = true)
    {
        TerritoryTestGrid grid = new(new CampaignMapGeometry(CampaignMapOffsetId: 0, Width: 9, Height: 9, WrapsHorizontally: wrapsHorizontally, WrapsVertically: wrapsVertically));

        if (withRival)
        {
            grid.Own(8, 8, _RIVAL);
        }

        return grid;
    }

    /// <summary>Rings the single tile (2, 2) with all eight of its neighbours, owned or claimed by one Player.</summary>
    /// <param name="settlementId">The Settlement the ring is owned through, to show that an absorbed interior does not inherit it.</param>
    private static TerritoryTestGrid RingedTile(bool owned, int ringPlayerId = _PLAYER, int? settlementId = null)
    {
        TerritoryTestGrid grid = NewGrid();

        foreach ((int x, int y) in Ring())
        {
            if (owned)
            {
                grid.Own(x, y, ringPlayerId, settlementId);
            }
            else
            {
                grid.Claim(x, y, ringPlayerId);
            }
        }

        return grid;
    }

    /// <summary>The eight tiles around (2, 2).</summary>
    private static IEnumerable<(int X, int Y)> Ring()
    {
        for (int y = 1; y <= 3; y++)
        {
            for (int x = 1; x <= 3; x++)
            {
                if ((x, y) != (2, 2))
                {
                    yield return (x, y);
                }
            }
        }
    }

    private static TerritoryWorkingSet Absorb(TerritoryTestGrid grid)
    {
        TerritoryWorkingSet working = grid.ToWorkingSet(_PLAYER, _RIVAL);
        TerritoryEnclosure.Absorb(working);

        return working;
    }

    [Test, Description("A complete ring of one Player's owned tiles absorbs the ground inside it, which is the whole point of the rule.")]
    public void ShouldAbsorbAnInteriorRingedByOwnedTiles()
    {
        TerritoryWorkingSet working = Absorb(RingedTile(owned: true));

        Assert.That(working.GetOwner((2, 2)), Is.EqualTo(_PLAYER));
    }

    [Test, Description("Enclosure grants bare ownership, so the interior carries no Settlement even where the whole ring around it does.")]
    public void ShouldAbsorbAnInteriorWithNoSettlement()
    {
        TerritoryTestGrid grid = RingedTile(owned: true, settlementId: 7);

        TerritoryChanges changes = grid.Resolve(_PLAYER);

        Assert.That(changes.OwnershipWrites[grid.IdOf(2, 2)].OwnedBySettlementId, Is.Null);
    }

    [Test, Description("A ring of claims yields a claimed interior: land is held only as firmly as the land behind it.")]
    public void ShouldAbsorbAnInteriorAsClaimedWhenTheRingIsOnlyClaimed()
    {
        TerritoryWorkingSet working = Absorb(RingedTile(owned: false));

        Assert.Multiple(() =>
        {
            Assert.That(working.GetOwner((2, 2)), Is.Null);
            Assert.That(working.GetClaimants((2, 2)), Is.EquivalentTo(new[] { _PLAYER }));
        });
    }

    [Test, Description("The interior takes the weakest tier in the ring, so one merely claimed tile in an otherwise owned ring makes the whole grant a claim.")]
    public void ShouldTakeTheWeakestTierInTheRing()
    {
        TerritoryTestGrid grid = NewGrid();

        // Seven owned tiles and one claimed, which is the weakest tier anywhere in the ring.
        foreach ((int x, int y) in Ring())
        {
            if ((x, y) == (1, 1))
            {
                grid.Claim(x, y, _PLAYER);

                continue;
            }

            grid.Own(x, y, _PLAYER);
        }

        TerritoryWorkingSet working = Absorb(grid);

        Assert.Multiple(() =>
        {
            Assert.That(working.GetOwner((2, 2)), Is.Null);
            Assert.That(working.GetClaimants((2, 2)), Is.EquivalentTo(new[] { _PLAYER }));
        });
    }

    [Test, Description("A rival tile on the boundary blocks absorption outright, however little of the boundary it forms.")]
    public void ShouldRefuseAbsorptionWhenARivalIsOnTheBoundary()
    {
        TerritoryTestGrid grid = RingedTile(owned: true).Own(1, 1, _RIVAL);

        TerritoryWorkingSet working = Absorb(grid);

        Assert.That(working.GetOwner((2, 2)), Is.Null);
    }

    [Test, Description("A ring with a diagonal gap is not a ring: the seal is tested on movement's adjacency so it cannot be walked through at a corner.")]
    public void ShouldRefuseAbsorptionThroughADiagonalGap()
    {
        TerritoryTestGrid grid = NewGrid();

        // The four orthogonal neighbours only, leaving all four corners open.
        grid.Own(2, 1, _PLAYER).Own(2, 3, _PLAYER).Own(1, 2, _PLAYER).Own(3, 2, _PLAYER);

        TerritoryWorkingSet working = Absorb(grid);

        Assert.That(working.GetOwner((2, 2)), Is.Null);
    }

    [Test, Description("The Void seals a ring because nothing can cross it, so a boundary part Void and part one Player's ground still absorbs.")]
    public void ShouldSealARingWithTheVoid()
    {
        TerritoryTestGrid grid = RingedTile(owned: true);
        grid.Void(1, 1);

        TerritoryWorkingSet working = Absorb(grid);

        Assert.That(working.GetOwner((2, 2)), Is.EqualTo(_PLAYER));
    }

    [Test, Description("Impassable terrain seals exactly as the Void does, which is what lets the rule be stated once rather than naming mountains separately.")]
    public void ShouldSealARingWithAMountain()
    {
        TerritoryTestGrid grid = RingedTile(owned: true);
        grid.Mountain(1, 1);

        TerritoryWorkingSet working = Absorb(grid);

        Assert.That(working.GetOwner((2, 2)), Is.EqualTo(_PLAYER));
    }

    [Test, Description("A mountain on a frontier is not itself absorbed while wilderness lies beyond it: you do not own the far slope of the wall that shelters you.")]
    public void ShouldNotAbsorbAFrontierMountain()
    {
        TerritoryTestGrid grid = NewGrid();

        // One owned tile with a mountain beside it, and open ground past the mountain.
        grid.Own(1, 1, _PLAYER);
        grid.Mountain(2, 1);

        TerritoryWorkingSet working = Absorb(grid);

        Assert.That(working.GetOwner((2, 1)), Is.Null);
    }

    [Test, Description("A mountain ringed entirely by owned tiles becomes owned, because a mountain inside a border is yours even though nothing can walk it.")]
    public void ShouldAbsorbAMountainRingedByOwnedTiles()
    {
        TerritoryTestGrid grid = RingedTile(owned: true);
        grid.Mountain(2, 2);

        TerritoryWorkingSet working = Absorb(grid);

        Assert.That(working.GetOwner((2, 2)), Is.EqualTo(_PLAYER));
    }

    [Test, Description("The same mountain ringed only by claims is left unclaimed, because a claimed mountain carries no hop and would lapse the Round it arrived.")]
    public void ShouldLeaveARingedMountainUnclaimedWhenTheRingIsOnlyClaimed()
    {
        TerritoryTestGrid grid = RingedTile(owned: false);
        grid.Mountain(2, 2);

        TerritoryWorkingSet working = Absorb(grid);

        Assert.Multiple(() =>
        {
            Assert.That(working.GetOwner((2, 2)), Is.Null);
            Assert.That(working.GetClaimants((2, 2)), Is.Empty);
        });
    }

    [Test, Description("A chain of mountains falls one link at a time, so the impassable sweep has to repeat until nothing more is taken.")]
    public void ShouldAbsorbAChainOfMountainsOverSuccessivePasses()
    {
        TerritoryTestGrid grid = NewGrid();

        // A 1 x 2 mountain corridor at (2, 2) and (3, 2), ringed by owned ground. The outer mountain is only enclosed once the inner one is owned.
        grid.Mountain(2, 2).Mountain(3, 2);

        for (int y = 1; y <= 3; y++)
        {
            for (int x = 1; x <= 4; x++)
            {
                if ((x, y) != (2, 2) && (x, y) != (3, 2))
                {
                    grid.Own(x, y, _PLAYER);
                }
            }
        }

        TerritoryWorkingSet working = Absorb(grid);

        Assert.Multiple(() =>
        {
            Assert.That(working.GetOwner((2, 2)), Is.EqualTo(_PLAYER));
            Assert.That(working.GetOwner((3, 2)), Is.EqualTo(_PLAYER));
        });
    }

    [TestCase(RiverWidth.Medium, Description = "A Medium river is a natural boundary and seals.")]
    [TestCase(RiverWidth.Large, Description = "So does a Large one.")]
    [Test, Description("A river edge seals an enclosure boundary, so a pocket closed on three sides by territory and on the fourth by a river is absorbed.")]
    public void ShouldSealAPocketWithARiver(RiverWidth width)
    {
        TerritoryTestGrid grid = NewGrid();

        // (2, 2) is owned on the north, west and south; its east side is a river, and the ground beyond is open.
        grid.Own(1, 1, _PLAYER).Own(2, 1, _PLAYER).Own(1, 2, _PLAYER).Own(1, 3, _PLAYER).Own(2, 3, _PLAYER);
        grid.River(2, 2, MoveDirection.East, width);

        // The two diagonals to the east would otherwise leak, so they carry the same river on their own east side.
        grid.River(2, 1, MoveDirection.East, width).River(2, 3, MoveDirection.East, width);

        TerritoryWorkingSet working = Absorb(grid);

        Assert.That(working.GetOwner((2, 2)), Is.EqualTo(_PLAYER));
    }

    [Test, Description("A trickle is not a boundary, so an edge narrower than Medium does not seal and the pocket stays open.")]
    public void ShouldNotSealAPocketWithNoRiver()
    {
        TerritoryTestGrid grid = NewGrid();

        grid.Own(1, 1, _PLAYER).Own(2, 1, _PLAYER).Own(1, 2, _PLAYER).Own(1, 3, _PLAYER).Own(2, 3, _PLAYER);

        TerritoryWorkingSet working = Absorb(grid);

        Assert.That(working.GetOwner((2, 2)), Is.Null);
    }

    [Test, Description("A river that merely ends at a corner leaves one way round it clear, so the diagonal is not sealed and the region escapes.")]
    public void ShouldNotSealADiagonalWhereARiverOnlyEndsAtTheCorner()
    {
        TerritoryTestGrid grid = NewGrid();

        // Everything but the south-east diagonal of (2, 2) is closed, and a single river edge is laid so that only one of the two ways round that corner crosses it.
        grid.Own(1, 1, _PLAYER).Own(2, 1, _PLAYER).Own(3, 1, _PLAYER);
        grid.Own(1, 2, _PLAYER).Own(1, 3, _PLAYER);
        grid.River(2, 2, MoveDirection.East, RiverWidth.Large);
        grid.River(2, 2, MoveDirection.South, RiverWidth.None);

        TerritoryWorkingSet working = Absorb(grid);

        Assert.That(working.GetOwner((2, 2)), Is.Null, "One clear way round the corner means the ring is not closed.");
    }

    [Test, Description("A rival across a sealing river never vetoes, because the seal removes the adjacency and they are not on the region's boundary at all.")]
    public void ShouldIgnoreARivalBeyondASealingRiver()
    {
        TerritoryTestGrid grid = NewGrid();

        grid.Own(1, 1, _PLAYER).Own(2, 1, _PLAYER).Own(1, 2, _PLAYER).Own(1, 3, _PLAYER).Own(2, 3, _PLAYER);
        grid.River(2, 2, MoveDirection.East, RiverWidth.Large);
        grid.River(2, 1, MoveDirection.East, RiverWidth.Large).River(2, 3, MoveDirection.East, RiverWidth.Large);

        // The rival sits directly across the river from the pocket.
        grid.Own(3, 2, _RIVAL);

        TerritoryWorkingSet working = Absorb(grid);

        Assert.That(working.GetOwner((2, 2)), Is.EqualTo(_PLAYER));
    }

    [Test, Description("A bay ringed by one Player's coast is absorbed, because water is ordinary terrain for ownership rather than a hole in the map.")]
    public void ShouldAbsorbABayRingedByOneCoast()
    {
        TerritoryTestGrid grid = RingedTile(owned: true);
        grid.Water(2, 2);

        TerritoryWorkingSet working = Absorb(grid);

        Assert.That(working.GetOwner((2, 2)), Is.EqualTo(_PLAYER));
    }

    [Test, Description("A pocket open to the ocean is never enclosed, because the region grows out into water nobody rings - which is why open sea belongs to nobody.")]
    public void ShouldNotAbsorbWaterOpenToTheOcean()
    {
        TerritoryTestGrid grid = NewGrid();

        // A coastal strip of owned land, with the whole east of the map water.
        for (int y = 0; y < 9; y++)
        {
            grid.Own(0, y, _PLAYER);

            for (int x = 1; x < 9; x++)
            {
                grid.Water(x, y);
            }
        }

        TerritoryWorkingSet working = Absorb(grid);

        Assert.That(working.GetOwner((4, 4)), Is.Null);
    }

    [Test, Description("An abandoned tile on the boundary refuses the region, so razing a settlement does not hand its hinterland to whoever rings it next.")]
    public void ShouldRefuseAbsorptionWhenAbandonedGroundIsOnTheBoundary()
    {
        TerritoryTestGrid grid = RingedTile(owned: true);
        grid.Abandon(1, 1, _RIVAL);

        TerritoryWorkingSet working = Absorb(grid);

        Assert.That(working.GetOwner((2, 2)), Is.Null);
    }

    [Test, Description("A lone Settlement on an otherwise empty map absorbs nothing, because the edge of the world is not territory anyone can ring against.")]
    public void ShouldAbsorbNothingForALoneSettlementOnAnEmptyMap()
    {
        TerritoryTestGrid grid = NewGrid(withRival: false);

        // A village's three-by-three and nothing else anywhere. Every other boundary of the remaining wilderness is the map's own border.
        for (int y = 3; y <= 5; y++)
        {
            for (int x = 3; x <= 5; x++)
            {
                grid.Own(x, y, _PLAYER, settlementId: 1);
            }
        }

        TerritoryChanges changes = grid.Resolve(_PLAYER, _RIVAL);

        Assert.That(changes.OwnershipWrites, Is.Empty, "Founding one village must not hand its owner the rest of the world.");
    }

    [Test, Description("A lone village on a world that wraps both ways absorbs nothing, because the rest of such a world meets itself the long way round rather than being encircled.")]
    public void ShouldAbsorbNothingForALoneSettlementOnAWrappingWorld()
    {
        // The live map's own shape: 30 x 20, wrapping on both axes, where a village once took 590 of the other 591 tiles.
        TerritoryTestGrid grid = new(new CampaignMapGeometry(CampaignMapOffsetId: 1, Width: 30, Height: 20, WrapsHorizontally: true, WrapsVertically: true));

        for (int y = 9; y <= 11; y++)
        {
            for (int x = 14; x <= 16; x++)
            {
                grid.Own(x, y, _PLAYER, settlementId: 1);
            }
        }

        TerritoryChanges changes = grid.Resolve(_PLAYER, _RIVAL);

        Assert.That(changes.OwnershipWrites, Is.Empty, "Founding one village must not hand its owner the planet.");
    }

    [Test, Description("A pocket a Player really has encircled on a wrapping world is still absorbed, so bounding the rule does not cost it its purpose.")]
    public void ShouldStillAbsorbATruePocketOnAWrappingWorld()
    {
        TerritoryTestGrid grid = new(new CampaignMapGeometry(CampaignMapOffsetId: 1, Width: 30, Height: 20, WrapsHorizontally: true, WrapsVertically: true));

        foreach ((int x, int y) in Ring())
        {
            grid.Own(x, y, _PLAYER);
        }

        TerritoryWorkingSet working = grid.ToWorkingSet(_PLAYER, _RIVAL);
        TerritoryEnclosure.Absorb(working);

        Assert.That(working.GetOwner((2, 2)), Is.EqualTo(_PLAYER));
    }

    [Test, Description("A region running off a non-wrapping edge is open however much of the rest of it one Player holds, since nothing was encircled.")]
    public void ShouldRefuseARegionThatTouchesANonWrappingEdge()
    {
        TerritoryTestGrid grid = NewGrid(withRival: false);

        // A wall sealing off the western two columns, which would be a pocket if the map's border counted as a seal.
        for (int y = 0; y < 9; y++)
        {
            grid.Own(2, y, _PLAYER);
        }

        TerritoryWorkingSet working = Absorb(grid);

        Assert.That(working.GetOwner((0, 4)), Is.Null);
    }

    [Test, Description("A band girdling a wrapping world absorbs nothing between its walls, because that strip wraps too - the deliberate price of the rule that stops a lone village taking the planet.")]
    public void ShouldRefuseAStripThatGirdlesAWrappingWorld()
    {
        TerritoryTestGrid grid = NewGrid(wrapsHorizontally: true, wrapsVertically: true, withRival: false);

        // Two full columns of owned ground. The strip between them is walled east and west, but it runs all the way round the world north to south.
        for (int y = 0; y < 9; y++)
        {
            grid.Own(0, y, _PLAYER).Own(8, y, _PLAYER);
        }

        TerritoryWorkingSet working = Absorb(grid);

        Assert.That(working.GetOwner((4, 4)), Is.Null, "Walling a strip that wraps is not encircling it, and allowing it would re-admit the lone-village case.");
    }

    [Test, Description("A rival anywhere in that strip refuses it as well, so the wrapping case needs no reasoning of its own on top of the bounded-region rule.")]
    public void ShouldRefuseAGirdlingStripWhenARivalHoldsSomeOfIt()
    {
        TerritoryTestGrid grid = NewGrid(wrapsHorizontally: true, wrapsVertically: true, withRival: false);

        for (int y = 0; y < 9; y++)
        {
            grid.Own(0, y, _PLAYER).Own(8, y, _PLAYER);
        }

        grid.Own(4, 4, _RIVAL);

        TerritoryWorkingSet working = Absorb(grid);

        Assert.That(working.GetOwner((4, 5)), Is.Null);
    }
}
