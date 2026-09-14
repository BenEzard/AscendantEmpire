using AscendantEmpire.Application.Features.Maps.Biomes.Services;
using AscendantEmpire.Application.Helpers.ScaledNumerics;

namespace AscendantEmpire.Tests.Features.Maps.Biomes;

/// <summary>
/// Tests for <see cref="SustenanceValueCalculator"/>: the formula that turns a biome's base sustenance value and a Player's summed modifiers into their effective value.
/// </summary>
/// <remarks>
/// <para>
/// No database - the calculator is pure. Summing the modifiers (and which ones apply) is the reader's job, and is covered in <c>PlayerSustenanceValueReaderTests</c>.
/// </para>
/// <para>
/// Every case starts from a base of 400, so a result that scaled the flat part, or skipped a floor, lands on a number no correct calculation could.
/// </para>
/// </remarks>
[TestFixture]
internal class SustenanceValueCalculatorTests
{
    private static readonly ScaledAmount _BASE_VALUE = ScaledAmount.FromWhole(400);

    [Test, Description("With no modifiers the effective value is the biome's base, since every Player starts from the same biome values.")]
    public void ShouldReturnTheBaseWhenThereAreNoModifiers()
    {
        ScaledAmount? effectiveValue = SustenanceValueCalculator.CalculateEffectiveValue(_BASE_VALUE, ScaledAmount.Zero, ScaledPercent.Zero);

        Assert.That(effectiveValue, Is.EqualTo(ScaledAmount.FromWhole(400)));
    }

    [Test, Description("A flat modifier is added straight onto the base.")]
    public void ShouldAddTheFlatTotalWhenOnlyAFlatModifierApplies()
    {
        ScaledAmount? effectiveValue = SustenanceValueCalculator.CalculateEffectiveValue(_BASE_VALUE, ScaledAmount.FromWhole(50), ScaledPercent.Zero);

        Assert.That(effectiveValue, Is.EqualTo(ScaledAmount.FromWhole(450)));
    }

    [Test, Description("A percentage modifier adds that share of the base: +25% of 400 is 100.")]
    public void ShouldAddTheShareOfTheBaseWhenOnlyAPercentageModifierApplies()
    {
        ScaledAmount? effectiveValue = SustenanceValueCalculator.CalculateEffectiveValue(_BASE_VALUE, ScaledAmount.Zero, ScaledPercent.FromWholePercent(25));

        Assert.That(effectiveValue, Is.EqualTo(ScaledAmount.FromWhole(500)));
    }

    [Test, Description("The percentage scales the base only, never the flat part: 400 + 25% of 400 + 50 is 550, where scaling the flat part too would give 562.5.")]
    public void ShouldNotScaleTheFlatTotalWhenBothKindsApply()
    {
        ScaledAmount? effectiveValue = SustenanceValueCalculator.CalculateEffectiveValue(_BASE_VALUE, ScaledAmount.FromWhole(50), ScaledPercent.FromWholePercent(25));

        Assert.That(effectiveValue, Is.EqualTo(ScaledAmount.FromWhole(550)));
    }

    [Test, Description("A resource the biome does not have stays absent whatever the modifiers say, because null means it can never gain a value.")]
    public void ShouldReturnNullWhenTheBaseIsNull()
    {
        ScaledAmount? effectiveValue = SustenanceValueCalculator.CalculateEffectiveValue(null, ScaledAmount.FromWhole(50), ScaledPercent.FromWholePercent(25));

        Assert.That(effectiveValue, Is.Null);
    }

    [Test, Description("Percentage penalties are floored at -100%, so -150% removes the base and no more, and a +50 flat bonus still leaves 50 rather than nothing.")]
    public void ShouldFloorThePercentageTotalAtMinusOneHundredPercentWhenPenaltiesExceedIt()
    {
        ScaledAmount? effectiveValue = SustenanceValueCalculator.CalculateEffectiveValue(_BASE_VALUE, ScaledAmount.FromWhole(50), ScaledPercent.FromWholePercent(-150));

        Assert.That(effectiveValue, Is.EqualTo(ScaledAmount.FromWhole(50)));
    }

    [Test, Description("The effective value is floored at 0, so flat penalties larger than the base cannot leave a negative amount of food on a tile.")]
    public void ShouldFloorTheEffectiveValueAtZeroWhenFlatPenaltiesExceedTheBase()
    {
        ScaledAmount? effectiveValue = SustenanceValueCalculator.CalculateEffectiveValue(_BASE_VALUE, ScaledAmount.FromWhole(-500), ScaledPercent.Zero);

        Assert.That(effectiveValue, Is.EqualTo(ScaledAmount.Zero));
    }
}
