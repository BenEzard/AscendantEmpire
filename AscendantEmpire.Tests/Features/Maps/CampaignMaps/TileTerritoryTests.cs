using AscendantEmpire.Application.Features.Maps.CampaignMaps.Classes;
using AscendantEmpire.BackendContracts.CampaignMaps;

namespace AscendantEmpire.Tests.Features.Maps.CampaignMaps;

/// <summary>
/// Pure unit tests for <see cref="TileTerritory"/> — no database, because the whole pass is state in and a diff out.
/// </summary>
/// <remarks>
/// The rules themselves are pinned by <see cref="TerritoryEnclosureTests"/> and <see cref="ClaimSupplyRangeTests"/>. What is pinned here is the orchestration: the order
/// the two run in, and the fact that the result is a diff rather than a stream of events - which is what makes the pass safe to run twice.
/// </remarks>
[TestFixture]
internal class TileTerritoryTests
{
    private const int _PLAYER = 1;
    private const int _RIVAL = 2;

    /// <summary>
    /// A grid with both Players holding a far corner, which is what every real Game looks like.
    /// </summary>
    /// <remarks>
    /// Both corners matter, because a non-wrapping map edge seals exactly as the Void does: one Player alone on an otherwise empty map rings the whole of the rest of it
    /// and absorbs it, which is consistent but would make every test here about that instead of about the pass's ordering. With two Players on the open region's
    /// boundary it belongs to neither, which is the design's own reasoning - "a game has at least two Players, so that always applies."
    /// </remarks>
    private static TerritoryTestGrid NewGrid(int width = 15, int height = 15)
    {
        TerritoryTestGrid grid = new(new CampaignMapGeometry(CampaignMapOffsetId: 0, Width: width, Height: height, WrapsHorizontally: false, WrapsVertically: false));

        return grid.Own(0, 0, _PLAYER).Own(width - 1, height - 1, _RIVAL);
    }

    [Test, Description("A claim enclosure grants is judged by the supply range in the same pass, which is why enclosure has to run first - the range is what caps how deep an interior can be held.")]
    public void ShouldJudgeAnEnclosureClaimByTheSupplyRangeInTheSamePass()
    {
        TerritoryTestGrid grid = NewGrid();

        // An owned anchor, and a ring of claims around (3, 3) tight enough that the interior it grants is within three hops of that anchor.
        grid.Own(1, 3, _PLAYER);

        for (int y = 2; y <= 4; y++)
        {
            for (int x = 2; x <= 4; x++)
            {
                if ((x, y) != (3, 3))
                {
                    grid.Claim(x, y, _PLAYER);
                }
            }
        }

        TerritoryChanges changes = grid.Resolve(_PLAYER, _RIVAL);

        Assert.Multiple(() =>
        {
            Assert.That(changes.ClaimsToAdd.Contains((grid.IdOf(3, 3), _PLAYER)), Is.True, "The interior is granted as a claim, the weakest tier in its ring.");
            Assert.That(changes.ClaimsToRemove.Contains((grid.IdOf(3, 3), _PLAYER)), Is.False, "And it is in range, so the same pass does not take it away again.");
        });
    }

    [Test, Description("A claim enclosure grants and the supply range then takes back produces no delta at all, so a wide pocket does not insert and delete the same row every Round.")]
    public void ShouldWriteNothingForAClaimGrantedAndLapsedInOnePass()
    {
        TerritoryTestGrid grid = NewGrid(width: 21, height: 21);

        // A ring of claims around a pocket far wider than three hops: the middle is absorbed as claimed and then lapses, because it cannot reach owned ground.
        for (int x = 2; x <= 18; x++)
        {
            grid.Claim(x, 2, _PLAYER).Claim(x, 18, _PLAYER);
        }

        for (int y = 2; y <= 18; y++)
        {
            grid.Claim(2, y, _PLAYER).Claim(18, y, _PLAYER);
        }

        TerritoryChanges changes = grid.Resolve(_PLAYER, _RIVAL);

        Assert.Multiple(() =>
        {
            Assert.That(changes.ClaimsToAdd.Contains((grid.IdOf(10, 10), _PLAYER)), Is.False, "Nothing is written for the middle of the pocket.");
            Assert.That(changes.ClaimsToRemove.Contains((grid.IdOf(10, 10), _PLAYER)), Is.False, "And nothing is deleted for it either.");
            Assert.That(changes.HasClaimsWrites.ContainsKey(grid.IdOf(10, 10)), Is.False, "So the flag does not move on it.");
        });
    }

    [Test, Description("A tile flagged as claimed with no claim rows behind it is repaired, because the pass holds the whole claim set and the flag is only a cache of it.")]
    public void ShouldRepairAStaleHasClaimsFlag()
    {
        TerritoryTestGrid grid = NewGrid();
        grid.SetHasClaimsFlag(5, 5, hasClaims: true);

        TerritoryChanges changes = grid.Resolve(_PLAYER, _RIVAL);

        Assert.That(changes.HasClaimsWrites[grid.IdOf(5, 5)], Is.False);
    }

    [Test, Description("The flag is set in the same diff as the rows whenever a claim is granted, so the junction and the cache are never observably out of step.")]
    public void ShouldSetHasClaimsAlongsideAGrantedClaim()
    {
        TerritoryTestGrid grid = NewGrid();

        // A ring of claims tight enough that the interior holds: the ring is within three hops of nothing owned, so an owned anchor is given to it.
        grid.Own(1, 1, _PLAYER);

        for (int y = 1; y <= 3; y++)
        {
            for (int x = 1; x <= 3; x++)
            {
                if ((x, y) != (2, 2) && (x, y) != (1, 1))
                {
                    grid.Claim(x, y, _PLAYER);
                }
            }
        }

        TerritoryChanges changes = grid.Resolve(_PLAYER, _RIVAL);

        Assert.Multiple(() =>
        {
            Assert.That(changes.ClaimsToAdd.Contains((grid.IdOf(2, 2), _PLAYER)), Is.True);
            Assert.That(changes.HasClaimsWrites[grid.IdOf(2, 2)], Is.True);
        });
    }

    [Test, Description("Running the pass twice on the same state writes nothing the second time, which is what lets it live in a pipeline with no transaction and no per-Round stamp.")]
    public void ShouldWriteNothingOnASecondRunOverTheSameState()
    {
        TerritoryTestGrid grid = NewGrid();

        for (int y = 1; y <= 3; y++)
        {
            for (int x = 1; x <= 3; x++)
            {
                if ((x, y) != (2, 2))
                {
                    grid.Own(x, y, _PLAYER);
                }
            }
        }

        TerritoryChanges first = grid.Resolve(_PLAYER, _RIVAL);

        Assert.That(first.IsEmpty, Is.False, "The first run has the interior to absorb.");

        // Apply what the first run decided, then run again over the result.
        grid.Own(2, 2, _PLAYER);

        Assert.That(grid.Resolve(_PLAYER, _RIVAL).IsEmpty, Is.True);
    }

    [Test, Description("A Round in which no border moved writes nothing at all, which is much the commonest outcome and must not cost a single statement.")]
    public void ShouldWriteNothingWhenNoBorderMoved()
    {
        Assert.That(NewGrid().Resolve(_PLAYER, _RIVAL).IsEmpty, Is.True);
    }

    [Test, Description("Players are walked in the order they are given, so where two could want one tile the earlier in the turn queue takes it rather than whoever a dictionary named first.")]
    public void ShouldWalkPlayersInTheOrderGiven()
    {
        TerritoryTestGrid grid = NewGrid();

        TerritoryWorkingSet working = grid.ToWorkingSet(_RIVAL, _PLAYER);

        Assert.That(working.PlayerIds, Is.EqualTo(new[] { _RIVAL, _PLAYER }).AsCollection);
    }
}
