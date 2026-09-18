using AscendantEmpire.Application.Features.Maps.Depletion.Services;
using AscendantEmpire.Application.Helpers.ScaledNumerics;

namespace AscendantEmpire.Tests.Features.Maps.Depletion;

/// <summary>
/// Tests for <see cref="TileRegenerationCalculator"/>: how one resource's <c>Taken</c> amount on a tile changes at the end of a Round.
/// </summary>
/// <remarks>
/// No database - the calculator is pure. Which tiles and resources are regenerated, and whether a tile was fed on, are the end-of-Round process's to decide and are covered
/// in <c>RegenerateTileSustenanceTests</c>. Most cases use the design doc's illustrative Hunt <c>Base</c> of 80 and a Regeneration Percent of 10%, so 8 regrows each Round.
/// </remarks>
[TestFixture]
internal class TileRegenerationCalculatorTests
{
    private const decimal _PERCENT = 0.1m;

    private static readonly ScaledAmount _BASE = ScaledAmount.FromWhole(80);

    [Test, Description("A partly depleted tile that is fed on regrows by the percentage of Base: 30 taken of 80 at 10% regrows by 8.")]
    public void ShouldRegrowAPartlyDepletedTileThatIsFedOn()
    {
        ScaledAmount taken = TileRegenerationCalculator.Regenerate(ScaledAmount.FromWhole(30), _BASE, _PERCENT, fedOn: true);

        Assert.That(taken, Is.EqualTo(ScaledAmount.FromWhole(22)));
    }

    [Test, Description("A bare tile regrows even while it is fed on, so a Force camping on it keeps getting the percentage of Base each Round.")]
    public void ShouldRegrowABareTileThatIsFedOn()
    {
        ScaledAmount taken = TileRegenerationCalculator.Regenerate(ScaledAmount.FromWhole(80), _BASE, _PERCENT, fedOn: true);

        Assert.That(taken, Is.EqualTo(ScaledAmount.FromWhole(72)));
    }

    [Test, Description("While fed on, regrowth pays off Taken above Base from a stronger Player first, rather than handing their surplus back.")]
    public void ShouldPayOffTakenAboveBaseFirstWhileFedOn()
    {
        ScaledAmount taken = TileRegenerationCalculator.Regenerate(ScaledAmount.FromWhole(100), _BASE, _PERCENT, fedOn: true);

        Assert.That(taken, Is.EqualTo(ScaledAmount.FromWhole(92)));
    }

    [TestCase(true, Description = "Regrowth is capped at Base on a tile that is fed on: 5 taken with 8 regrowing leaves Taken at 0, never below it.")]
    [TestCase(false, Description = "Regrowth is capped at Base on a tile left alone as well.")]
    public void ShouldNeverRegrowTakenBelowZero(bool fedOn)
    {
        ScaledAmount taken = TileRegenerationCalculator.Regenerate(ScaledAmount.FromWhole(5), _BASE, _PERCENT, fedOn);

        Assert.That(taken, Is.EqualTo(ScaledAmount.Zero));
    }

    [TestCase(100, Description = "Taken above Base is forgiven on a tile left alone: the 10% floor (72) beats regrowth (92).")]
    [TestCase(80, Description = "A bare tile left alone recovers to 10% of Base, the same as regrowth at 10%.")]
    public void ShouldApplyTheTenPercentFloorToATileNotFedOn(long takenWhole)
    {
        ScaledAmount taken = TileRegenerationCalculator.Regenerate(ScaledAmount.FromWhole(takenWhole), _BASE, _PERCENT, fedOn: false);

        Assert.That(taken, Is.EqualTo(ScaledAmount.FromWhole(72)));
    }

    [TestCase(73, 0.1, 65, Description = "Below 10% of Base, a tile left alone still regrows past the floor when regrowth takes it further.")]
    [TestCase(80, 0.5, 40, Description = "A fast-regrowing biome left alone regrows its full percentage rather than stopping at the 10% floor.")]
    public void ShouldKeepRegrowthWhenItBeatsTheFloor(long takenWhole, decimal percent, long expectedTakenWhole)
    {
        ScaledAmount taken = TileRegenerationCalculator.Regenerate(ScaledAmount.FromWhole(takenWhole), _BASE, percent, fedOn: false);

        Assert.That(taken, Is.EqualTo(ScaledAmount.FromWhole(expectedTakenWhole)));
    }

    [Test, Description("The floor only applies to a tile nobody fed on: a stronger Player camping on it gets only the regrowth, not their surplus back.")]
    public void ShouldNotApplyTheFloorToATileThatIsFedOn()
    {
        ScaledAmount fedOnTaken = TileRegenerationCalculator.Regenerate(ScaledAmount.FromWhole(100), _BASE, _PERCENT, fedOn: true);
        ScaledAmount leftAloneTaken = TileRegenerationCalculator.Regenerate(ScaledAmount.FromWhole(100), _BASE, _PERCENT, fedOn: false);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(fedOnTaken, Is.EqualTo(ScaledAmount.FromWhole(92)));
            Assert.That(leftAloneTaken, Is.EqualTo(ScaledAmount.FromWhole(72)));
        }
    }

    [Test, Description("A base of 0 regrows nothing, so anything taken stays while the tile is fed on.")]
    public void ShouldKeepAZeroBaseTileBareWhileFedOn()
    {
        ScaledAmount taken = TileRegenerationCalculator.Regenerate(ScaledAmount.FromWhole(5), ScaledAmount.Zero, _PERCENT, fedOn: true);

        Assert.That(taken, Is.EqualTo(ScaledAmount.FromWhole(5)));
    }

    [Test, Description("A base of 0 resets to Taken 0 after a Round left alone, since 10% of nothing is nothing.")]
    public void ShouldResetAZeroBaseTileWhenNotFedOn()
    {
        ScaledAmount taken = TileRegenerationCalculator.Regenerate(ScaledAmount.FromWhole(5), ScaledAmount.Zero, _PERCENT, fedOn: false);

        Assert.That(taken, Is.EqualTo(ScaledAmount.Zero));
    }

    [Test, Description("Regrowth is rounded down to the thousandth, against the tile: 10% of a Base of 0.015 regrows 0.001, not 0.0015 rounded up.")]
    public void ShouldRoundRegrowthDown()
    {
        ScaledAmount taken = TileRegenerationCalculator.Regenerate(ScaledAmount.FromRaw(10), ScaledAmount.FromRaw(15), _PERCENT, fedOn: true);

        Assert.That(taken, Is.EqualTo(ScaledAmount.FromRaw(9)));
    }

    [Test, Description("0.9 x Base is rounded up to the thousandth, against the tile: a Base of 0.015 refills to Taken 0.014, not 0.0135 rounded down.")]
    public void ShouldRoundTheRefillUp()
    {
        ScaledAmount taken = TileRegenerationCalculator.Regenerate(ScaledAmount.FromRaw(15), ScaledAmount.FromRaw(15), _PERCENT, fedOn: false);

        Assert.That(taken, Is.EqualTo(ScaledAmount.FromRaw(14)));
    }

    [Test, Description("The design doc's worked example: Default strips the tile, Advanced camps on it, then it is left alone and recovers, Round by Round.")]
    public void ShouldFollowTheWorkedExample()
    {
        // Default takes all 80; the Round ends with the tile fed on, and 8 regrows.
        ScaledAmount afterFirstRound = TileRegenerationCalculator.Regenerate(ScaledAmount.FromWhole(80), _BASE, _PERCENT, fedOn: true);

        // Advanced, with Hunt 100, arrives and takes the 28 they can see; the Round ends fed on, and the 8 regrown pays off their surplus.
        ScaledAmount afterSecondRound = TileRegenerationCalculator.Regenerate(afterFirstRound + ScaledAmount.FromWhole(28), _BASE, _PERCENT, fedOn: true);

        // Advanced leaves; the Round ends with nobody fed on the tile, so the 10% floor (72) beats regrowth (84) and forgives the rest of their surplus.
        ScaledAmount afterThirdRound = TileRegenerationCalculator.Regenerate(afterSecondRound, _BASE, _PERCENT, fedOn: false);

        // Left alone for another Round, the tile regrows 8.
        ScaledAmount afterFourthRound = TileRegenerationCalculator.Regenerate(afterThirdRound, _BASE, _PERCENT, fedOn: false);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(afterFirstRound, Is.EqualTo(ScaledAmount.FromWhole(72)));
            Assert.That(afterSecondRound, Is.EqualTo(ScaledAmount.FromWhole(92)));
            Assert.That(afterThirdRound, Is.EqualTo(ScaledAmount.FromWhole(72)));
            Assert.That(afterFourthRound, Is.EqualTo(ScaledAmount.FromWhole(64)));
        }
    }

    [Test, Description("Inputs that would break the rules - a negative Taken or Base, or a percentage outside 0 to 1 - are refused rather than producing a nonsense Taken.")]
    public void ShouldRejectInvalidInputs()
    {
        using (Assert.EnterMultipleScope())
        {
            Assert.That(() => TileRegenerationCalculator.Regenerate(ScaledAmount.FromRaw(-1), _BASE, _PERCENT, fedOn: true), Throws.TypeOf<ArgumentOutOfRangeException>());
            Assert.That(() => TileRegenerationCalculator.Regenerate(ScaledAmount.Zero, ScaledAmount.FromRaw(-1), _PERCENT, fedOn: true), Throws.TypeOf<ArgumentOutOfRangeException>());
            Assert.That(() => TileRegenerationCalculator.Regenerate(ScaledAmount.Zero, _BASE, -0.1m, fedOn: true), Throws.TypeOf<ArgumentOutOfRangeException>());
            Assert.That(() => TileRegenerationCalculator.Regenerate(ScaledAmount.Zero, _BASE, 1.001m, fedOn: true), Throws.TypeOf<ArgumentOutOfRangeException>());
        }
    }
}
