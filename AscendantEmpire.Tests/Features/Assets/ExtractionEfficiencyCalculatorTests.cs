using AscendantEmpire.Application.Features.Assets.Services;
using AscendantEmpire.Application.Helpers.ScaledNumerics;

namespace AscendantEmpire.Tests.Features.Assets;

/// <summary>
/// Tests for <see cref="ExtractionEfficiencyCalculator"/>: <c>Start + Start × ΣPercent + ΣFlat</c>, floored at zero.
/// </summary>
[TestFixture]
internal class ExtractionEfficiencyCalculatorTests
{
    private static readonly ScaledPercent _FIVE_PERCENT = ScaledPercent.FromWholePercent(5);

    #region Modifiers

    [Test, Description("With no modifiers the efficiency is the extraction path's starting figure, which is the before-any-technology state.")]
    public void ShouldReturnStartWhenNoModifiers()
    {
        ScaledPercent efficiency = ExtractionEfficiencyCalculator.CalculateEfficiency(_FIVE_PERCENT, ScaledPercent.Zero, ScaledPercent.Zero);

        Assert.That(efficiency, Is.EqualTo(_FIVE_PERCENT));
    }

    [Test, Description("Flat points add straight onto the start, which is how PrimitiveCrafting1's +95 takes 5% to 100%.")]
    public void ShouldAddFlatPointsWhenFlatGranted()
    {
        ScaledPercent efficiency = ExtractionEfficiencyCalculator.CalculateEfficiency(_FIVE_PERCENT, ScaledPercent.FromWholePercent(95), ScaledPercent.Zero);

        Assert.That(efficiency, Is.EqualTo(ScaledPercent.FromWholePercent(100)));
    }

    [Test, Description("Flat points from several sources sum rather than the largest winning, so a later +20 tier takes the Player past 100%.")]
    public void ShouldSumFlatPointsWhenSeveralGranted()
    {
        ScaledPercent flatTotal = ScaledPercent.FromWholePercent(95) + ScaledPercent.FromWholePercent(20);

        ScaledPercent efficiency = ExtractionEfficiencyCalculator.CalculateEfficiency(_FIVE_PERCENT, flatTotal, ScaledPercent.Zero);

        Assert.That(efficiency, Is.EqualTo(ScaledPercent.FromWholePercent(120)));
    }

    [Test, Description("A percentage scales the start alone, so +100% of a 5% start is 10%, not 105%.")]
    public void ShouldScaleStartOnlyWhenPercentGranted()
    {
        ScaledPercent efficiency = ExtractionEfficiencyCalculator.CalculateEfficiency(_FIVE_PERCENT, ScaledPercent.Zero, ScaledPercent.FromWholePercent(100));

        Assert.That(efficiency, Is.EqualTo(ScaledPercent.FromWholePercent(10)));
    }

    #endregion

    #region Floor

    [Test, Description("Penalties larger than the start floor the efficiency at zero, so a take can never go negative.")]
    public void ShouldFloorAtZeroWhenPenaltiesExceedStart()
    {
        ScaledPercent efficiency = ExtractionEfficiencyCalculator.CalculateEfficiency(_FIVE_PERCENT, ScaledPercent.FromWholePercent(-20), ScaledPercent.Zero);

        Assert.That(efficiency, Is.EqualTo(ScaledPercent.Zero));
    }

    #endregion
}
