using AscendantEmpire.Application.Features.Maps.CampaignMaps.Classes;
using AscendantEmpire.BackendContracts.CampaignMaps;
using AscendantEmpire.Domain.Enums.Maps;

namespace AscendantEmpire.Tests.Features.Maps.CampaignMaps;

/// <summary>
/// Pure unit tests for <see cref="ClaimSupplyRange"/> — no database, because the walk reads only the tile snapshots it is handed.
/// </summary>
/// <remarks>
/// <para>
/// These pin the Claim supply range of <c>docs/design/campaign-map/tile-ownership.md</c>, which is the whole anti-land-grab rule: a claim holds only while it reaches
/// owned ground within three hops of the Player's own land.
/// </para>
/// <para>
/// The two rules it deliberately does <i>not</i> carry are pinned here too: impassability stops a chain, and a river never does.
/// </para>
/// </remarks>
[TestFixture]
internal class ClaimSupplyRangeTests
{
    private const int _PLAYER = 1;
    private const int _RIVAL = 2;

    private static TerritoryTestGrid NewGrid(bool wrapsHorizontally = false)
    {
        return new TerritoryTestGrid(new CampaignMapGeometry(CampaignMapOffsetId: 0, Width: 12, Height: 5, WrapsHorizontally: wrapsHorizontally, WrapsVertically: false));
    }

    /// <summary>Lapses the Player's out-of-range claims and reports whether the claim at <paramref name="coord"/> survived.</summary>
    private static bool ClaimSurvives(TerritoryTestGrid grid, (int X, int Y) coord)
    {
        TerritoryWorkingSet working = grid.ToWorkingSet(_PLAYER, _RIVAL);
        ClaimSupplyRange.Lapse(working, _PLAYER, new HashSet<int> { _PLAYER });

        return working.GetClaimants(coord).Contains(_PLAYER);
    }

    /// <summary>An owned anchor at (0, 2) with a chain of the Player's claims running east from it.</summary>
    private static TerritoryTestGrid ChainFromAnchor(int claimLength)
    {
        TerritoryTestGrid grid = NewGrid();
        grid.Own(0, 2, _PLAYER);

        for (int x = 1; x <= claimLength; x++)
        {
            grid.Claim(x, 2, _PLAYER);
        }

        return grid;
    }

    [TestCase(1, Description = "One hop from owned ground.")]
    [TestCase(2, Description = "Two hops.")]
    [TestCase(3, Description = "Three hops, which is the limit.")]
    [Test, Description("A claim within three hops of owned land through the Player's own territory holds, because real settlement is following it.")]
    public void ShouldKeepAClaimWithinRange(int hops)
    {
        Assert.That(ClaimSurvives(ChainFromAnchor(hops), (hops, 2)), Is.True);
    }

    [Test, Description("A claim four hops out lapses, which is what stops a scout keeping a distant chokepoint.")]
    public void ShouldLapseAClaimBeyondRange()
    {
        Assert.That(ClaimSurvives(ChainFromAnchor(4), (4, 2)), Is.False);
    }

    [Test, Description("An owned mountain carries no hop, so it is a dead end rather than a bridge and a claim behind it cannot be supplied through it.")]
    public void ShouldNotCarryAChainThroughAMountain()
    {
        TerritoryTestGrid grid = NewGrid();

        // Anchor, then a wall of mountains across the whole column, then the Player's claim beyond it.
        grid.Own(0, 2, _PLAYER);

        for (int y = 0; y < 5; y++)
        {
            grid.Mountain(1, y).Own(1, y, _PLAYER);
        }

        grid.Claim(2, 2, _PLAYER);

        Assert.That(ClaimSurvives(grid, (2, 2)), Is.False);
    }

    [Test, Description("The same claim holds where a passable detour reaches it within three hops, so a mountain range turns a border rather than stopping it.")]
    public void ShouldCarryAChainRoundAMountain()
    {
        TerritoryTestGrid grid = NewGrid();

        grid.Own(0, 2, _PLAYER);
        grid.Mountain(1, 2).Own(1, 2, _PLAYER);

        // The detour: one step diagonally past the mountain, then back in.
        grid.Claim(1, 1, _PLAYER).Claim(2, 2, _PLAYER);

        Assert.That(ClaimSurvives(grid, (2, 2)), Is.True);
    }

    [Test, Description("A claimed mountain is never reached by the walk at all, so it lapses the Round it arrives - which is why a mountain is only ever owned.")]
    public void ShouldLapseAClaimOnAMountainImmediately()
    {
        TerritoryTestGrid grid = NewGrid();

        grid.Own(0, 2, _PLAYER);
        grid.Mountain(1, 2).Claim(1, 2, _PLAYER);

        Assert.That(ClaimSurvives(grid, (1, 2)), Is.False);
    }

    [Test, Description("Owned ground that cannot be walked cannot anchor a chain either, so a mountain is not a source however firmly it is held.")]
    public void ShouldNotAnchorAChainOnAnOwnedMountain()
    {
        TerritoryTestGrid grid = NewGrid();

        grid.Mountain(0, 2).Own(0, 2, _PLAYER);
        grid.Claim(1, 2, _PLAYER);

        Assert.That(ClaimSurvives(grid, (1, 2)), Is.False);
    }

    [Test, Description("Water is passable, so a chain runs across a narrow strait of owned sea with no water clause in the rule at all.")]
    public void ShouldCarryAChainAcrossAStraitOfOwnedWater()
    {
        TerritoryTestGrid grid = NewGrid();

        grid.Own(0, 2, _PLAYER);
        grid.Water(1, 2).Own(1, 2, _PLAYER);
        grid.Water(2, 2).Own(2, 2, _PLAYER);
        grid.Water(3, 2).Claim(3, 2, _PLAYER);

        Assert.That(ClaimSurvives(grid, (3, 2)), Is.True);
    }

    [Test, Description("A claim far out in unowned ocean lapses, because nothing out there is owned to anchor it - which is how open sea ends up belonging to nobody.")]
    public void ShouldLapseAClaimInOpenOcean()
    {
        TerritoryTestGrid grid = NewGrid();

        grid.Own(0, 2, _PLAYER);

        for (int x = 1; x < 12; x++)
        {
            grid.Water(x, 2);
        }

        grid.Claim(8, 2, _PLAYER);

        Assert.That(ClaimSurvives(grid, (8, 2)), Is.False);
    }

    [Test, Description("A river between a claim and its anchor does not lapse it: rivers shape enclosure and impassability shapes supply, and neither borrows the other's rule.")]
    public void ShouldCarryAChainAcrossALargeRiver()
    {
        TerritoryTestGrid grid = ChainFromAnchor(1);
        grid.River(0, 2, MoveDirection.East, RiverWidth.Large);

        Assert.That(ClaimSurvives(grid, (1, 2)), Is.True);
    }

    [Test, Description("A rival's ground never carries a chain, so a claim reachable only through a neighbour's territory lapses.")]
    public void ShouldNotCarryAChainThroughARivalsGround()
    {
        TerritoryTestGrid grid = NewGrid();

        grid.Own(0, 2, _PLAYER);

        // A full-height wall of the rival's claims, with the Player's own claim beyond it.
        for (int y = 0; y < 5; y++)
        {
            grid.Claim(1, y, _RIVAL);
        }

        grid.Claim(2, 2, _PLAYER);

        Assert.That(ClaimSurvives(grid, (2, 2)), Is.False);
    }

    [Test, Description("A chain reaching its anchor the short way round a wrapping seam holds, because the walk uses the same neighbour arithmetic movement does.")]
    public void ShouldCarryAChainAcrossAWrappingSeam()
    {
        TerritoryTestGrid grid = NewGrid(wrapsHorizontally: true);

        grid.Own(0, 2, _PLAYER);
        grid.Claim(11, 2, _PLAYER);

        Assert.That(ClaimSurvives(grid, (11, 2)), Is.True);
    }

    [Test, Description("Lapsing one Player's claim leaves a rival's claim on the same tile alone, because claims are held per Player and are not exclusive.")]
    public void ShouldLeaveARivalsClaimOnTheSameTile()
    {
        TerritoryTestGrid grid = NewGrid();

        // Out of range for the Player, but the rival has an anchor right beside it.
        grid.Claim(8, 2, _PLAYER).Claim(8, 2, _RIVAL);
        grid.Own(0, 2, _PLAYER).Own(7, 2, _RIVAL);

        TerritoryWorkingSet working = grid.ToWorkingSet(_PLAYER, _RIVAL);
        ClaimSupplyRange.Lapse(working, _PLAYER, new HashSet<int> { _PLAYER });

        Assert.That(working.GetClaimants((8, 2)), Is.EquivalentTo(new[] { _RIVAL }));
    }
}
