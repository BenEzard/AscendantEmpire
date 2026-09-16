using AscendantEmpire.Application.Features.Maps.Depletion.Services;
using AscendantEmpire.Application.Helpers.ScaledNumerics;
using AscendantEmpire.Domain.Enums.Resources;

namespace AscendantEmpire.Tests.Features.Maps.Depletion;

/// <summary>
/// Tests for <see cref="ForceSupportCalculator"/>: which support level a tile's available food earns against a food need.
/// </summary>
/// <remarks>
/// No database - the calculator is pure. Which Forces make up the need, and what the pool holds, are covered in <c>TileSupportEvaluatorTests</c>.
/// </remarks>
[TestFixture]
internal class ForceSupportCalculatorTests
{
    [TestCase(100, 500, TileSupportLevel.Plenty, Description = "Five Turns of food is plenty.")]
    [TestCase(100, 200, TileSupportLevel.Plenty, Description = "Exactly two Turns of food is plenty: the boundary is inclusive.")]
    [TestCase(100, 199.999, TileSupportLevel.OneTurn, Description = "A thousandth short of two Turns is only one Turn.")]
    [TestCase(100, 100, TileSupportLevel.OneTurn, Description = "Exactly one Turn of food is one Turn: the boundary is inclusive.")]
    [TestCase(100, 99.999, TileSupportLevel.Insufficient, Description = "A thousandth short of one Turn cannot feed the whole Force.")]
    [TestCase(100, 0.001, TileSupportLevel.Insufficient, Description = "Any food at all is insufficient rather than exhausted.")]
    public void ShouldClassifyAvailableFoodAgainstTheNeed(decimal need, decimal available, TileSupportLevel expected)
    {
        TileSupportLevel? level = ForceSupportCalculator.Classify(ScaledAmount.FromDecimal(need), ScaledAmount.FromDecimal(available));

        Assert.That(level, Is.EqualTo(expected));
    }

    [TestCase(100, Description = "A tile with nothing left is exhausted for Forces standing on it.")]
    [TestCase(0, Description = "A tile with nothing left is exhausted even with nobody on it, so a stripped tile always reads as stripped.")]
    public void ShouldBeExhaustedWhenNothingIsAvailable(decimal need)
    {
        TileSupportLevel? level = ForceSupportCalculator.Classify(ScaledAmount.FromDecimal(need), ScaledAmount.Zero);

        Assert.That(level, Is.EqualTo(TileSupportLevel.Exhausted));
    }

    [Test, Description("A tile with food but nobody to feed has no level, so nothing is coloured.")]
    public void ShouldReturnNullWhenThereIsNoNeedAndFoodIsAvailable()
    {
        TileSupportLevel? level = ForceSupportCalculator.Classify(ScaledAmount.Zero, ScaledAmount.FromWhole(50));

        Assert.That(level, Is.Null);
    }

    [Test, Description("A negative need is a defect in the caller and is refused rather than classified.")]
    public void ShouldThrowForANegativeNeed()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => ForceSupportCalculator.Classify(ScaledAmount.FromWhole(-1), ScaledAmount.FromWhole(50)));
    }

    [Test, Description("A negative amount available is a defect in the caller and is refused rather than classified.")]
    public void ShouldThrowForANegativeAmountAvailable()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => ForceSupportCalculator.Classify(ScaledAmount.FromWhole(1), ScaledAmount.FromWhole(-50)));
    }
}
