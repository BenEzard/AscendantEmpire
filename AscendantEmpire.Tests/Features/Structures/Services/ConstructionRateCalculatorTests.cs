using AscendantEmpire.Application.Features.Structures.Services;

namespace AscendantEmpire.Tests.Features.Structures.Services;

/// <summary>
/// Tests for <see cref="ConstructionRateCalculator"/>: how fast a Force builds, and how many Turns that makes a given Structure.
/// </summary>
/// <remarks>
/// No database. The formula is <c>Power × HealthyCount × MIN(1, movement remaining / allowance)</c>, and the numbers below are the human body's — Power 7.0, allowance 1.0 —
/// so they read as the calibration does: 200 healthy humans on a full Turn make 1,400 points.
/// </remarks>
[TestFixture]
internal class ConstructionRateCalculatorTests
{
    /// <summary>The human body's Power: 7.0, raw Scale-1,000.</summary>
    private const long _POWER = 7_000;

    /// <summary>The human body's campaign movement allowance: 1.0, raw Scale-1,000.</summary>
    private const long _ALLOWANCE = 1_000;

    [Test, Description("200 healthy humans on a full turn make 1,400 points, which is the anchor every build point total in the catalogue is calibrated against.")]
    public void ShouldMakeFourteenHundredPointsForTwoHundredHumansOnAFullTurn()
    {
        long rate = ConstructionRateCalculator.CalculateBuildPointsPerTurn(_POWER, healthyCount: 200, _ALLOWANCE, _ALLOWANCE);

        Assert.That(rate, Is.EqualTo(1_400_000), "1,400.0 points, raw Scale-1,000.");
    }

    [Test, Description("The rate scales with the number of healthy, so twice the hands build twice as fast and half build half as fast.")]
    [TestCase(400, 2_800_000, Description = "Twice the anchor force.")]
    [TestCase(200, 1_400_000, Description = "The anchor force.")]
    [TestCase(100, 700_000, Description = "Half the anchor force, and the smallest allowed a village.")]
    [TestCase(25, 175_000, Description = "The smallest force allowed a camp.")]
    [TestCase(1, 7_000, Description = "One person.")]
    public void ShouldScaleWithTheHealthyCount(int healthyCount, long expectedRate)
    {
        Assert.That(ConstructionRateCalculator.CalculateBuildPointsPerTurn(_POWER, healthyCount, _ALLOWANCE, _ALLOWANCE), Is.EqualTo(expectedRate));
    }

    [Test, Description("The rate scales with how much of the turn is left, so a force that marched to the spot builds less than one that was already standing there.")]
    [TestCase(1_000, 1_400_000, Description = "A whole turn left.")]
    [TestCase(500, 700_000, Description = "Half a turn left.")]
    [TestCase(250, 350_000, Description = "A quarter.")]
    [TestCase(1, 1_400, Description = "A thousandth of a turn still earns a thousandth of the work.")]
    public void ShouldScaleWithTheMovementLeft(long remaining, long expectedRate)
    {
        Assert.That(ConstructionRateCalculator.CalculateBuildPointsPerTurn(_POWER, healthyCount: 200, remaining, _ALLOWANCE), Is.EqualTo(expectedRate));
    }

    /// <summary>
    /// Unspent movement carries into the next Turn on top of the allowance, so a Force can open its Turn holding more than a full one. Without the cap it would out-build a
    /// fresh Force for no reason anybody designed.
    /// </summary>
    [Test, Description("Movement carried over from a previous turn is worth no more than a full turn, so a force cannot build faster than a fresh one.")]
    public void ShouldCapTheMovementFractionAtAWholeTurn()
    {
        long carriedOver = ConstructionRateCalculator.CalculateBuildPointsPerTurn(_POWER, healthyCount: 200, _ALLOWANCE * 5, _ALLOWANCE);
        long fresh = ConstructionRateCalculator.CalculateBuildPointsPerTurn(_POWER, healthyCount: 200, _ALLOWANCE, _ALLOWANCE);

        Assert.That(carriedOver, Is.EqualTo(fresh));
    }

    /// <summary>
    /// Movement can go negative: a Force holding any movement at all may enter a tile and pays its whole cost. Such a Force contributes nothing, which is the floor the
    /// settle rules already apply — founding needs movement left.
    /// </summary>
    [Test, Description("A force in movement debt contributes nothing, falling to the one-point floor rather than going negative.")]
    [TestCase(0, Description = "Exactly spent.")]
    [TestCase(-500, Description = "In debt.")]
    public void ShouldContributeNothingWithNoMovementLeft(long remaining)
    {
        long rate = ConstructionRateCalculator.CalculateBuildPointsPerTurn(_POWER, healthyCount: 200, remaining, _ALLOWANCE);

        Assert.That(rate, Is.EqualTo(ConstructionRateCalculator.MINIMUM_BUILD_POINTS_PER_TURN_RAW));
    }

    /// <summary>
    /// A rate of zero is a Structure that can never finish, which would be a permanent hole in the map. The floor makes such a build absurdly slow instead.
    /// </summary>
    [Test, Description("A force that would do no work at all is floored at one point, so a founding always finishes eventually rather than never.")]
    [TestCase(0, 200, Description = "A body with no power authored.")]
    [TestCase(7_000, 0, Description = "Nobody healthy.")]
    [TestCase(-7_000, 200, Description = "Negative power, which nothing should author.")]
    public void ShouldFloorAtOnePoint(long power, int healthyCount)
    {
        long rate = ConstructionRateCalculator.CalculateBuildPointsPerTurn(power, healthyCount, _ALLOWANCE, _ALLOWANCE);

        Assert.That(rate, Is.EqualTo(ConstructionRateCalculator.MINIMUM_BUILD_POINTS_PER_TURN_RAW));
    }

    /// <summary>
    /// A template with no allowance authored cannot have the fraction worked out at all. Credited with a full Turn rather than none: the Force is plainly standing there able
    /// to work, and a divide by zero or a silent nothing would both be worse answers.
    /// </summary>
    [Test, Description("A force whose template authors no movement allowance is credited with a full turn, rather than dividing by zero or earning nothing.")]
    public void ShouldCreditAFullTurnWhenNoAllowanceIsAuthored()
    {
        long rate = ConstructionRateCalculator.CalculateBuildPointsPerTurn(_POWER, healthyCount: 200, campaignMovementRemainingRaw: 0, campaignMovementPointsRaw: 0);

        Assert.That(rate, Is.EqualTo(1_400_000));
    }

    [Test, Description("Precision survives a part-spent turn: the multiply happens before the divide, so a tenth of a turn is a tenth of the work rather than nothing.")]
    public void ShouldKeepPrecisionOnAPartSpentTurn()
    {
        long tenth = ConstructionRateCalculator.CalculateBuildPointsPerTurn(_POWER, healthyCount: 1, campaignMovementRemainingRaw: 100, campaignMovementPointsRaw: 1_000);

        Assert.That(tenth, Is.EqualTo(700), "0.7 points, not 0 and not 1.");
    }

    // ── turns to complete ─────────────────────────────────────────────────────

    [Test, Description("The village calibration in turns: 2,800 points at the anchor force's rate is two turns, and halving the rate doubles it.")]
    [TestCase(1_400_000, 2, Description = "200 healthy humans on a full turn.")]
    [TestCase(700_000, 4, Description = "Half the force, or half the turn.")]
    [TestCase(350_000, 8, Description = "A quarter of either.")]
    [TestCase(2_800_000, 1, Description = "Twice the force finishes within one turn.")]
    public void ShouldTurnAVillagesWorkIntoTurns(long rate, int expectedTurns)
    {
        Assert.That(ConstructionRateCalculator.CalculateTurnsToComplete(2_800_000, rate), Is.EqualTo(expectedTurns));
    }

    /// <summary>
    /// A Structure is finished at the <b>end</b> of a Turn and usable the Turn after, so a build with a tenth of its work left still costs a whole Turn. Rounding down would
    /// promise the player a Turn sooner than the construction process will actually deliver.
    /// </summary>
    [Test, Description("A part turn of work left still costs a whole turn, so the figure the player is quoted matches what construction will do.")]
    public void ShouldRoundAPartTurnUp()
    {
        Assert.That(ConstructionRateCalculator.CalculateTurnsToComplete(buildPointsRaw: 1_001, buildPointsPerTurnRaw: 1_000), Is.EqualTo(2));
    }

    [Test, Description("A kind that needs no work is finished at the end of the turn it was founded in, which is one turn rather than none.")]
    [TestCase(0L, Description = "No work authored.")]
    [TestCase(-1L, Description = "Negative work, which nothing should author.")]
    public void ShouldFinishWorklessStructuresInOneTurn(long buildPointsRequired)
    {
        Assert.That(ConstructionRateCalculator.CalculateTurnsToComplete(buildPointsRequired, 1_400_000), Is.EqualTo(1));
    }

    [Test, Description("A rate below the floor is treated as the floor here too, so the two methods cannot disagree about whether a build can finish.")]
    public void ShouldFloorTheRateWhenCountingTurns()
    {
        Assert.That(ConstructionRateCalculator.CalculateTurnsToComplete(10, buildPointsPerTurnRaw: 0), Is.EqualTo(10));
    }
}
