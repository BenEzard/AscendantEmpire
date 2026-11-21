using AscendantEmpire.Application.Features.Assets.Services;

namespace AscendantEmpire.Tests.Features.Assets;

/// <summary>
/// Tests for <see cref="WaterLineAllocator"/>: where the line falls, and who pays the units that do not divide.
/// </summary>
/// <remarks>
/// No database - the allocator is pure. What it decides reaches the game on two axes, as an expense spread across a network of banks and as a Force eating
/// what it carries, and both are pinned against their own rows elsewhere.
/// </remarks>
[TestFixture]
internal class WaterLineAllocatorTests
{
    [Test, Description("The fullest levels are cut down to a common line and the rest are left alone, so a small holding keeps what it has.")]
    public void ShouldCutTheFullestDownToACommonLine()
    {
        long[] deductions = WaterLineAllocator.Allocate([50, 30, 4], take: 40);

        Assert.That(deductions, Is.EqualTo(new long[] { 30, 10, 0 }), "Both of the full levels end on 20 and the level of 4 is untouched.");
    }

    [Test, Description("A take at or above everything the levels hold empties every one of them rather than levelling anything.")]
    public void ShouldEmptyEveryLevelWhenTheTakeIsTheWholeOfIt()
    {
        long[] deductions = WaterLineAllocator.Allocate([50, 30, 4], take: 84);

        Assert.That(deductions, Is.EqualTo(new long[] { 50, 30, 4 }));
    }

    [Test, Description("A take of nothing or less deducts nothing, so a need already met cannot cost a holding a unit.")]
    public void ShouldDeductNothingWhenThereIsNothingToTake()
    {
        long[] deductions = WaterLineAllocator.Allocate([50, 30], take: 0);

        Assert.That(deductions, Is.EqualTo(new long[] { 0, 0 }));
    }

    [Test, Description("A unit that cannot be split evenly comes off the level listed first, so the caller's own ordering is what decides it rather than chance.")]
    public void ShouldTakeTheLeftoverUnitFromTheLevelListedFirst()
    {
        long[] deductions = WaterLineAllocator.Allocate([10, 10], take: 1);

        Assert.That(deductions, Is.EqualTo(new long[] { 1, 0 }));
    }

    [Test, Description("A take spread across many equal levels still deducts exactly what was asked for, so nothing goes missing or appears in the rounding.")]
    public void ShouldDeductExactlyWhatWasAskedFor()
    {
        long[] deductions = WaterLineAllocator.Allocate([7, 7, 7, 7], take: 10);

        Assert.That(deductions.Sum(), Is.EqualTo(10));
    }

    [Test, Description("A level too low to reach the line pays nothing at all, which is the point of levelling over a proportional spread.")]
    public void ShouldLeaveALevelBelowTheLineAlone()
    {
        long[] deductions = WaterLineAllocator.Allocate([100, 2], take: 50);

        Assert.That(deductions, Is.EqualTo(new long[] { 50, 0 }));
    }
}
