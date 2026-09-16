using AscendantEmpire.Application.Features.Maps.Depletion.Services;
using AscendantEmpire.Application.Helpers.ScaledNumerics;

namespace AscendantEmpire.Tests.Features.Maps.Depletion;

/// <summary>
/// Tests for <see cref="TileRegenerationCalculator"/>: how one resource's <c>Taken</c> amount on a tile changes at the end of a Round.
/// </summary>
/// <remarks>
/// No database - the calculator is pure. Which tiles and resources are regenerated, and whether a tile was fed on, are the end-of-Round process's to decide and are covered
/// in <c>RegenerateTileSustenanceTests</c>. Most cases use the design doc's illustrative Hunt <c>Base</c> of 80 and <c>Rate</c> of 1.1.
/// </remarks>
[TestFixture]
internal class TileRegenerationCalculatorTests
{
    private const decimal _RATE = 1.1m;

    private static readonly ScaledAmount _BASE = ScaledAmount.FromWhole(80);

    [Test, Description("A partly depleted tile that is fed on regrows by the base view times (Rate - 1): 30 taken of 80 regrows by 5.")]
    public void ShouldRegrowAPartlyDepletedTileThatIsFedOn()
    {
        ScaledAmount taken = TileRegenerationCalculator.Regenerate(ScaledAmount.FromWhole(30), _BASE, _RATE, fedOn: true);

        Assert.That(taken, Is.EqualTo(ScaledAmount.FromWhole(25)));
    }

    [Test, Description("A tile fed on while just short of bare still regrows, since only Taken at or above Base holds it bare.")]
    public void ShouldRegrowATileFedOnJustBelowBase()
    {
        ScaledAmount taken = TileRegenerationCalculator.Regenerate(ScaledAmount.FromWhole(79), _BASE, _RATE, fedOn: true);

        Assert.That(taken, Is.EqualTo(ScaledAmount.FromDecimal(78.9m)));
    }

    [Test, Description("Regrowth is capped at Base, so a fast rate on a lightly depleted tile brings Taken to 0 and never below it.")]
    public void ShouldNeverRegrowTakenBelowZero()
    {
        // The base view of 70 at Rate 1.5 would regrow 35, more than the 10 taken.
        ScaledAmount taken = TileRegenerationCalculator.Regenerate(ScaledAmount.FromWhole(10), _BASE, 1.5m, fedOn: true);

        Assert.That(taken, Is.EqualTo(ScaledAmount.Zero));
    }

    [TestCase(80, Description = "A tile bare at base values stays bare while it is fed on.")]
    [TestCase(100, Description = "Taken above Base from a stronger Player is kept, so their surplus is not handed back on a tile that is meant to be bare.")]
    public void ShouldLeaveABareTileThatIsFedOnUnchanged(long takenWhole)
    {
        ScaledAmount taken = TileRegenerationCalculator.Regenerate(ScaledAmount.FromWhole(takenWhole), _BASE, _RATE, fedOn: true);

        Assert.That(taken, Is.EqualTo(ScaledAmount.FromWhole(takenWhole)));
    }

    [TestCase(73, Description = "Below 10% of Base, a tile left alone jumps to 10% and does not also regrow that Round.")]
    [TestCase(80, Description = "A bare tile left alone jumps to 10% of Base.")]
    [TestCase(100, Description = "Taken above Base is forgiven on a tile left alone, which drops straight to 10% of Base.")]
    public void ShouldRefillATileBelowTenPercentThatIsNotFedOn(long takenWhole)
    {
        ScaledAmount taken = TileRegenerationCalculator.Regenerate(ScaledAmount.FromWhole(takenWhole), _BASE, _RATE, fedOn: false);

        Assert.That(taken, Is.EqualTo(ScaledAmount.FromWhole(72)));
    }

    [TestCase(72, 71.2, Description = "Exactly 10% of Base is not below it, so the tile regrows rather than refilling.")]
    [TestCase(50, 47, Description = "A tile left alone at or above 10% regrows exactly as a fed-on one would.")]
    public void ShouldRegrowATileAtOrAboveTenPercentThatIsNotFedOn(long takenWhole, decimal expectedTaken)
    {
        ScaledAmount taken = TileRegenerationCalculator.Regenerate(ScaledAmount.FromWhole(takenWhole), _BASE, _RATE, fedOn: false);

        Assert.That(taken, Is.EqualTo(ScaledAmount.FromDecimal(expectedTaken)));
    }

    [Test, Description("A base of 0 is always bare at base values: anything taken stays while the tile is fed on.")]
    public void ShouldKeepAZeroBaseTileBareWhileFedOn()
    {
        ScaledAmount taken = TileRegenerationCalculator.Regenerate(ScaledAmount.FromWhole(5), ScaledAmount.Zero, _RATE, fedOn: true);

        Assert.That(taken, Is.EqualTo(ScaledAmount.FromWhole(5)));
    }

    [Test, Description("A base of 0 resets to Taken 0 after a Round left alone, since 10% of nothing is nothing.")]
    public void ShouldResetAZeroBaseTileWhenNotFedOn()
    {
        ScaledAmount taken = TileRegenerationCalculator.Regenerate(ScaledAmount.FromWhole(5), ScaledAmount.Zero, _RATE, fedOn: false);

        Assert.That(taken, Is.EqualTo(ScaledAmount.Zero));
    }

    [Test, Description("Regrowth is rounded down to the thousandth, against the tile: a base view of 0.007 at Rate 1.25 regrows 0.001, not 0.00175 rounded up.")]
    public void ShouldRoundRegrowthDown()
    {
        ScaledAmount taken = TileRegenerationCalculator.Regenerate(ScaledAmount.FromRaw(3), ScaledAmount.FromRaw(10), 1.25m, fedOn: true);

        Assert.That(taken, Is.EqualTo(ScaledAmount.FromRaw(2)));
    }

    [Test, Description("0.9 x Base is rounded up to the thousandth, against the tile: a Base of 0.015 refills to Taken 0.014, not 0.0135 rounded down.")]
    public void ShouldRoundTheRefillUp()
    {
        ScaledAmount taken = TileRegenerationCalculator.Regenerate(ScaledAmount.FromRaw(15), ScaledAmount.FromRaw(15), _RATE, fedOn: false);

        Assert.That(taken, Is.EqualTo(ScaledAmount.FromRaw(14)));
    }

    [Test, Description("The design doc's worked example: Default strips the tile, Advanced camps on it, then it is left alone and recovers, Round by Round.")]
    public void ShouldFollowTheWorkedExample()
    {
        // Default takes 30; the Round ends with the tile fed on.
        ScaledAmount afterFirstRound = TileRegenerationCalculator.Regenerate(ScaledAmount.FromWhole(30), _BASE, _RATE, fedOn: true);

        // Default takes the remaining 55; the Round ends fed on and bare.
        ScaledAmount afterSecondRound = TileRegenerationCalculator.Regenerate(afterFirstRound + ScaledAmount.FromWhole(55), _BASE, _RATE, fedOn: true);

        // Advanced, with Hunt 100, takes the 20 only they can reach; the Round ends fed on and bare.
        ScaledAmount afterThirdRound = TileRegenerationCalculator.Regenerate(afterSecondRound + ScaledAmount.FromWhole(20), _BASE, _RATE, fedOn: true);

        // Advanced leaves, and the tile is left alone for two Rounds.
        ScaledAmount afterFourthRound = TileRegenerationCalculator.Regenerate(afterThirdRound, _BASE, _RATE, fedOn: false);
        ScaledAmount afterFifthRound = TileRegenerationCalculator.Regenerate(afterFourthRound, _BASE, _RATE, fedOn: false);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(afterFirstRound, Is.EqualTo(ScaledAmount.FromWhole(25)));
            Assert.That(afterSecondRound, Is.EqualTo(ScaledAmount.FromWhole(80)));
            Assert.That(afterThirdRound, Is.EqualTo(ScaledAmount.FromWhole(100)));
            Assert.That(afterFourthRound, Is.EqualTo(ScaledAmount.FromWhole(72)));
            Assert.That(afterFifthRound, Is.EqualTo(ScaledAmount.FromDecimal(71.2m)));
        }
    }

    [Test, Description("Inputs that would break the rules - a negative Taken or Base, or a rate that shrinks food - are refused rather than producing a nonsense Taken.")]
    public void ShouldRejectInvalidInputs()
    {
        using (Assert.EnterMultipleScope())
        {
            Assert.That(() => TileRegenerationCalculator.Regenerate(ScaledAmount.FromRaw(-1), _BASE, _RATE, fedOn: true), Throws.TypeOf<ArgumentOutOfRangeException>());
            Assert.That(() => TileRegenerationCalculator.Regenerate(ScaledAmount.Zero, ScaledAmount.FromRaw(-1), _RATE, fedOn: true), Throws.TypeOf<ArgumentOutOfRangeException>());
            Assert.That(() => TileRegenerationCalculator.Regenerate(ScaledAmount.Zero, _BASE, 0.9m, fedOn: true), Throws.TypeOf<ArgumentOutOfRangeException>());
        }
    }
}
