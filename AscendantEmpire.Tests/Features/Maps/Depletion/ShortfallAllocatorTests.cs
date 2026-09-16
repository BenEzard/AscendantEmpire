using AscendantEmpire.Application.Features.Maps.Depletion.Services;
using AscendantEmpire.Application.Helpers.ScaledNumerics;

namespace AscendantEmpire.Tests.Features.Maps.Depletion;

/// <summary>
/// Tests for <see cref="ShortfallAllocator"/>: how a tile's pooled shortfall is shared across the Forces that fed on it.
/// </summary>
/// <remarks>
/// No database - the allocator is pure.
/// </remarks>
[TestFixture]
internal class ShortfallAllocatorTests
{
    [Test, Description("Each Force goes short in proportion to its troops, so a Force three times the size carries three times the shortfall.")]
    public void ShouldShareTheShortfallInProportionToTroops()
    {
        Dictionary<int, ScaledAmount> shares = ShortfallAllocator.Allocate(ScaledAmount.FromWhole(40), new Dictionary<int, long> { [1] = 300, [2] = 100 });

        using (Assert.EnterMultipleScope())
        {
            Assert.That(shares[1], Is.EqualTo(ScaledAmount.FromWhole(30)));
            Assert.That(shares[2], Is.EqualTo(ScaledAmount.FromWhole(10)));
        }
    }

    [Test, Description("Shares that do not divide evenly still sum exactly to the tile's shortfall, so no food goes missing or appears between the tile and its Forces.")]
    public void ShouldAllocateExactlyTheShortfall()
    {
        ScaledAmount shortfall = ScaledAmount.FromWhole(10);

        Dictionary<int, ScaledAmount> shares = ShortfallAllocator.Allocate(shortfall, new Dictionary<int, long> { [1] = 1, [2] = 1, [3] = 1 });

        Assert.That(shares.Values.Aggregate(ScaledAmount.Zero, (sum, share) => sum + share), Is.EqualTo(shortfall));
    }

    [Test, Description("The result depends only on the Forces and their troops, never on the order they are listed in, so processing order cannot decide who goes hungrier.")]
    public void ShouldNotDependOnTheOrderTheForcesAreListedIn()
    {
        ScaledAmount shortfall = ScaledAmount.FromWhole(10);

        Dictionary<int, ScaledAmount> forwards = ShortfallAllocator.Allocate(shortfall, new Dictionary<int, long> { [1] = 1, [2] = 1, [3] = 1 });
        Dictionary<int, ScaledAmount> backwards = ShortfallAllocator.Allocate(shortfall, new Dictionary<int, long> { [3] = 1, [2] = 1, [1] = 1 });

        Assert.That(backwards, Is.EquivalentTo(forwards));
    }

    [Test, Description("With no shortfall, every Force on the tile is reported as fed in full.")]
    public void ShouldAllocateNothingWhenThereIsNoShortfall()
    {
        Dictionary<int, ScaledAmount> shares = ShortfallAllocator.Allocate(ScaledAmount.Zero, new Dictionary<int, long> { [1] = 300, [2] = 100 });

        Assert.That(shares.Values, Is.All.EqualTo(ScaledAmount.Zero));
    }
}
