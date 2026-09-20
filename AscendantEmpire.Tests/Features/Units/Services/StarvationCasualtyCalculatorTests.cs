using AscendantEmpire.Application.Features.Units.Classes;
using AscendantEmpire.Application.Features.Units.Services;
using AscendantEmpire.Application.Helpers.ScaledNumerics;

namespace AscendantEmpire.Tests.Features.Units.Services;

/// <summary>
/// Tests for <see cref="StarvationCasualtyCalculator"/>: what a Turn without enough food costs a Force, and which of its troops pay.
/// </summary>
/// <remarks>
/// <para>
/// No database - the calculator is pure. The roll is fixed through <see cref="FixedRandom"/> wherever the split between dying and being wounded matters, so
/// nothing here depends on which values a real stream happens to produce.
/// </para>
/// <para>
/// Severity is 34% at total starvation and the dead share is drawn between 10% and 30% of the casualties. Both are private, so the figures are worked back
/// from the outputs rather than read off the constants - a test that imported them would pass whatever they were changed to.
/// </para>
/// </remarks>
[TestFixture]
internal class StarvationCasualtyCalculatorTests
{
    [Test, Description("A Force that got no food at all loses 34% of its troops, so the severity constant is what decides the scale of a total shortfall.")]
    public void ShouldLoseTheFullSeverityWhenNothingWasFed()
    {
        StarvationCasualties casualties = Calculate(troops: 1_000, needWhole: 1_000, shortfallWhole: 1_000);

        Assert.That(casualties.Total, Is.EqualTo(340));
    }

    [Test, Description("Half a Turn's food costs half of what no food costs, pinning the total as proportional to the shortfall rather than a flat rate.")]
    public void ShouldLoseInProportionToTheShortfall()
    {
        StarvationCasualties casualties = Calculate(troops: 1_000, needWhole: 1_000, shortfallWhole: 500);

        Assert.That(casualties.Total, Is.EqualTo(170));
    }

    [Test, Description("A large Force a little short of food loses nobody, because the total is floored - small shortfalls are meant to be survivable.")]
    public void ShouldLoseNobodyWhenTheShortfallFloorsToZero()
    {
        StarvationCasualties casualties = Calculate(troops: 100, needWhole: 100, shortfallWhole: 2);

        Assert.That(casualties, Is.EqualTo(StarvationCasualties.None));
    }

    [Test, Description("The roll decides only how the total splits, so the dead share applies to the casualties and the rest are wounded.")]
    public void ShouldSplitTheCasualtiesByTheRolledDeadShare()
    {
        StarvationCasualties casualties = Calculate(troops: 1_000, needWhole: 1_000, shortfallWhole: 1_000, deadSharePerThousand: 300);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(casualties.Killed, Is.EqualTo(102));
            Assert.That(casualties.Wounded, Is.EqualTo(238));
            Assert.That(casualties.Total, Is.EqualTo(340));
        }
    }

    [Test, Description("The dead count is floored and the wounded take the remainder, so the rounding error always favours the Player.")]
    public void ShouldRoundTheDeadDownAndGiveTheRemainderToTheWounded()
    {
        StarvationCasualties casualties = Calculate(troops: 21, needWhole: 21, shortfallWhole: 21, deadSharePerThousand: 100);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(casualties.Killed, Is.Zero, "0.7 of a man dies, which is nobody.");
            Assert.That(casualties.Wounded, Is.EqualTo(7));
        }
    }

    [Test, Description("A different roll moves troops between the dead and the wounded without changing how many the Force loses in total.")]
    public void ShouldKeepTheTotalWhicheverWayTheRollFalls()
    {
        StarvationCasualties gentle = Calculate(troops: 1_000, needWhole: 1_000, shortfallWhole: 1_000, deadSharePerThousand: 100);
        StarvationCasualties harsh = Calculate(troops: 1_000, needWhole: 1_000, shortfallWhole: 1_000, deadSharePerThousand: 300);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(harsh.Total, Is.EqualTo(gentle.Total));
            Assert.That(harsh.Killed, Is.GreaterThan(gentle.Killed));
        }
    }

    [Test, Description("A Force that fed in full takes nothing, so a well-fed Force handed to the calculator by mistake is harmless.")]
    public void ShouldTakeNothingWithNoShortfall()
    {
        StarvationCasualties casualties = Calculate(troops: 1_000, needWhole: 1_000, shortfallWhole: 0);

        Assert.That(casualties, Is.EqualTo(StarvationCasualties.None));
    }

    [Test, Description("A Force with no troops takes nothing rather than producing negative casualties.")]
    public void ShouldTakeNothingWhenTheForceHasNoTroops()
    {
        StarvationCasualties casualties = Calculate(troops: 0, needWhole: 100, shortfallWhole: 100);

        Assert.That(casualties, Is.EqualTo(StarvationCasualties.None));
    }

    [Test, Description("A shortfall larger than the need is treated as going wholly without, so bad input cannot push losses past the severity.")]
    public void ShouldCapTheShortfallAtTheNeed()
    {
        StarvationCasualties casualties = Calculate(troops: 1_000, needWhole: 1_000, shortfallWhole: 5_000);

        Assert.That(casualties.Total, Is.EqualTo(340));
    }

    // ── which troops pay ──────────────────────────────────────────────────────

    [Test, Description("Deaths are taken from the already-wounded before the healthy, because they are the men least able to survive another Turn.")]
    public void ShouldKillTheWoundedBeforeTheHealthy()
    {
        (int healthyCount, int injuredCount) = StarvationCasualtyCalculator.Apply(healthyCount: 50, injuredCount: 10, new StarvationCasualties(Killed: 4, Wounded: 0));

        using (Assert.EnterMultipleScope())
        {
            Assert.That(healthyCount, Is.EqualTo(50));
            Assert.That(injuredCount, Is.EqualTo(6));
        }
    }

    [Test, Description("Deaths reach the healthy once the wounded are gone, so a Force with few wounded still loses men.")]
    public void ShouldKillTheHealthyOnceTheWoundedAreExhausted()
    {
        (int healthyCount, int injuredCount) = StarvationCasualtyCalculator.Apply(healthyCount: 50, injuredCount: 2, new StarvationCasualties(Killed: 5, Wounded: 0));

        using (Assert.EnterMultipleScope())
        {
            Assert.That(injuredCount, Is.Zero);
            Assert.That(healthyCount, Is.EqualTo(47));
        }
    }

    [Test, Description("Wounds move healthy troops into the injured rather than removing them, so the Force keeps its numbers and loses its strength.")]
    public void ShouldWoundTheHealthy()
    {
        (int healthyCount, int injuredCount) = StarvationCasualtyCalculator.Apply(healthyCount: 50, injuredCount: 0, new StarvationCasualties(Killed: 0, Wounded: 8));

        using (Assert.EnterMultipleScope())
        {
            Assert.That(healthyCount, Is.EqualTo(42));
            Assert.That(injuredCount, Is.EqualTo(8));
        }
    }

    [Test, Description("A wound with no healthy man left to take it kills instead, so a wholly wounded Force starves to death rather than sitting there.")]
    public void ShouldKillWhenThereIsNobodyLeftToWound()
    {
        (int healthyCount, int injuredCount) = StarvationCasualtyCalculator.Apply(healthyCount: 0, injuredCount: 20, new StarvationCasualties(Killed: 1, Wounded: 3));

        using (Assert.EnterMultipleScope())
        {
            Assert.That(healthyCount, Is.Zero);
            Assert.That(injuredCount, Is.EqualTo(16), "One died outright and the three wounds had nobody healthy to take them.");
        }
    }

    [Test, Description("Casualties that account for every troop leave the Force empty, which is the state that has it destroyed.")]
    public void ShouldEmptyTheForceWhenEverybodyIsAccountedFor()
    {
        (int healthyCount, int injuredCount) = StarvationCasualtyCalculator.Apply(healthyCount: 3, injuredCount: 1, new StarvationCasualties(Killed: 4, Wounded: 0));

        using (Assert.EnterMultipleScope())
        {
            Assert.That(healthyCount, Is.Zero);
            Assert.That(injuredCount, Is.Zero);
        }
    }

    // ── arrangement ───────────────────────────────────────────────────────────

    private static StarvationCasualties Calculate(int troops, long needWhole, long shortfallWhole, int deadSharePerThousand = 200)
    {
        return StarvationCasualtyCalculator.Calculate(
            troops,
            ScaledAmount.FromWhole(needWhole),
            ScaledAmount.FromWhole(shortfallWhole),
            new FixedRandom(deadSharePerThousand));
    }

    /// <summary>
    /// A <see cref="Random"/> whose ranged draw always answers the same value, so a test can pin the split between the dead and the wounded.
    /// </summary>
    private sealed class FixedRandom(int value) : Random
    {
        public override int Next(int minValue, int maxValue)
        {
            return value;
        }
    }
}
