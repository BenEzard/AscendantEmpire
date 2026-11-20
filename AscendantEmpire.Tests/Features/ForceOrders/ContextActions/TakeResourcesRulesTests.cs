using AscendantEmpire.Application.Features.ForceOrders.ContextActions.Strategies;
using AscendantEmpire.Application.Helpers.ScaledNumerics;

namespace AscendantEmpire.Tests.Features.ForceOrders.ContextActions;

/// <summary>
/// Pure unit tests for <see cref="TakeResourcesRules"/> — the arithmetic behind how much a Force takes and what it turns into.
/// </summary>
/// <remarks>
/// These are the numbers the button, the dialog's promise and the write all read, so an error here would be an error the player sees twice: once as a
/// promise and once as a different outcome.
/// </remarks>
[TestFixture]
internal class TakeResourcesRulesTests
{
    #region Units sought

    [Test, Description("The per-individual rate is the whole of the take's size, so the raw figure must be the headcount times the rate, scaled.")]
    public void ShouldSeekTheRateInWholeUnitsPerHealthyIndividual()
    {
        long sought = TakeResourcesRules.CalculateUnitsSoughtRaw(30);

        // Read off the rate rather than pinned to a number, since the rate is tuning and stands in for a carry weight that does not exist yet.
        Assert.That(sought, Is.EqualTo(ScaledAmount.FromWhole(30L * TakeResourcesRules.UNITS_PER_HEALTHY_INDIVIDUAL).RawValue));
    }

    [Test, Description("A Force of nothing but casualties has no one to load up, so it seeks nothing rather than a negative or a default share.")]
    [TestCase(0, Description = "Nobody healthy at all.")]
    [TestCase(-5, Description = "Bad data, which must not invert into a credit.")]
    public void ShouldSeekNothingWithoutAHealthyIndividual(int healthyCount)
    {
        Assert.That(TakeResourcesRules.CalculateUnitsSoughtRaw(healthyCount), Is.Zero);
    }

    [Test, Description("A Force large enough to overflow a 32-bit multiply must still compute, since the headcount times the rate times the scale leaves an int behind at a plausible headcount.")]
    public void ShouldSeekTheRightAmountForALargeForce()
    {
        // The smallest headcount whose raw figure passes int.MaxValue at the current rate, so retuning the rate cannot quietly stop exercising the widening.
        int healthyCount = (int)(int.MaxValue / (TakeResourcesRules.UNITS_PER_HEALTHY_INDIVIDUAL * ScaledAmount.Scale)) + 1;

        long sought = TakeResourcesRules.CalculateUnitsSoughtRaw(healthyCount);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(sought, Is.GreaterThan(int.MaxValue));
            Assert.That(sought, Is.EqualTo(ScaledAmount.FromWhole((long)healthyCount * TakeResourcesRules.UNITS_PER_HEALTHY_INDIVIDUAL).RawValue));
        }
    }

    #endregion

    #region Asset amount

    [Test, Description("A yield's rate is itself fixed-point, so the scale has to come back out of the product or every take would credit a thousandfold.")]
    public void ShouldPriceATakeAtTheYieldsRate()
    {
        // 10 whole units taken, at 2.5 of the Asset per unit.
        long amount = TakeResourcesRules.CalculateAssetAmountRaw(10 * ScaledAmount.Scale, (long)(2.5 * ScaledAmount.Scale));

        Assert.That(amount, Is.EqualTo(25 * ScaledAmount.Scale));
    }

    [Test, Description("A Force allotted nothing, or a yield rated at nothing, must credit nothing rather than a rounding artefact.")]
    [TestCase(0L, 1_000L, Description = "Nothing taken.")]
    [TestCase(1_000L, 0L, Description = "A yield that gives nothing.")]
    [TestCase(-1_000L, 1_000L, Description = "Bad data, which must not invert into a credit.")]
    public void ShouldPriceNothingWhenEitherFigureIsNotPositive(long unitsTakenRaw, long amountPerUnit)
    {
        Assert.That(TakeResourcesRules.CalculateAssetAmountRaw(unitsTakenRaw, amountPerUnit), Is.Zero);
    }

    [Test, Description("The multiply is widened to Int128 precisely so a legitimate quantity at a legitimate rate cannot wrap into a negative balance.")]
    public void ShouldPriceALargeTakeWithoutOverflowing()
    {
        // A million whole units at 1,000 per unit: the unwidened product passes long.MaxValue by three orders of magnitude.
        long amount = TakeResourcesRules.CalculateAssetAmountRaw(1_000_000L * ScaledAmount.Scale, 1_000L * ScaledAmount.Scale);

        Assert.That(amount, Is.EqualTo(1_000_000_000L * ScaledAmount.Scale));
    }

    #endregion

    #region Planning shares

    private static TakeResourcesTaker Taker(int unitInstanceId, int healthyCount)
    {
        return new TakeResourcesTaker(unitInstanceId, $"Force {unitInstanceId}", healthyCount);
    }

    /// <summary>
    /// What a Force of this many healthy individuals can carry, raw.
    /// </summary>
    /// <remarks>
    /// These tests are about how a patch divides between Forces, not about the rate, so they size their patches off the rate rather than off a number a
    /// retuning would falsify. The rate itself is pinned above.
    /// </remarks>
    private static long Carries(int healthyCount)
    {
        return TakeResourcesRules.CalculateUnitsSoughtRaw(healthyCount);
    }

    [Test, Description("One Force on a plentiful patch takes everything it can carry, which is the ordinary case the figures above describe.")]
    public void ShouldGiveALoneForceEverythingItCanCarry()
    {
        IReadOnlyList<TakeResourcesShare> shares = TakeResourcesRules.PlanShares([Taker(7, 10)], Carries(10) * 100);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(shares, Has.Exactly(1).Items);
            Assert.That(shares[0].UnitsTakenRaw, Is.EqualTo(Carries(10)));
        }
    }

    [Test, Description("A patch smaller than one Force can carry is emptied, not overdrawn, since the quantity is what the handler then decrements.")]
    public void ShouldClampAShareToWhatThePatchHolds()
    {
        long available = Carries(10) / 4;

        IReadOnlyList<TakeResourcesShare> shares = TakeResourcesRules.PlanShares([Taker(7, 10)], available);

        Assert.That(shares[0].UnitsTakenRaw, Is.EqualTo(available));
    }

    [Test, Description("Forces are served in ascending id whatever order the client listed them, so the same order against the same patch always divides it the same way.")]
    public void ShouldServeForcesInAscendingIdWhateverOrderTheyArrive()
    {
        // A patch holding one and a half Forces' worth, against two Forces of equal size.
        long available = Carries(2) + (Carries(2) / 2);

        IReadOnlyList<TakeResourcesShare> shares = TakeResourcesRules.PlanShares([Taker(9, 2), Taker(4, 2)], available);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(shares.Select(share => share.Taker.UnitInstanceId), Is.EqualTo(new[] { 4, 9 }).AsCollection);
            Assert.That(shares[0].UnitsTakenRaw, Is.EqualTo(Carries(2)), "The lower id loads up first.");
            Assert.That(shares[1].UnitsTakenRaw, Is.EqualTo(available - Carries(2)), "The second gets only the remainder.");
        }
    }

    [Test, Description("A Force that arrives at an emptied patch is still reported, carrying a zero, because the player has to be told who came away empty-handed.")]
    public void ShouldReportAForceThatGetsNothing()
    {
        IReadOnlyList<TakeResourcesShare> shares = TakeResourcesRules.PlanShares([Taker(1, 1), Taker(2, 1)], Carries(1));

        using (Assert.EnterMultipleScope())
        {
            Assert.That(shares, Has.Exactly(2).Items);
            Assert.That(shares[0].UnitsTakenRaw, Is.EqualTo(Carries(1)));
            Assert.That(shares[1].UnitsTakenRaw, Is.Zero);
        }
    }

    [Test, Description("A patch already at nothing hands out nothing rather than a negative share, which would credit cargo and grow the patch.")]
    public void ShouldGiveNothingFromAnEmptyPatch()
    {
        IReadOnlyList<TakeResourcesShare> shares = TakeResourcesRules.PlanShares([Taker(1, 10)], 0);

        Assert.That(shares[0].UnitsTakenRaw, Is.Zero);
    }

    [Test, Description("The shares must sum to no more than the patch held, since their total is what comes off the instance's quantity in one write.")]
    public void ShouldNeverPlanMoreThanThePatchHolds()
    {
        long available = (Carries(2) * 2) + (Carries(2) / 4);

        IReadOnlyList<TakeResourcesShare> shares = TakeResourcesRules.PlanShares([Taker(1, 2), Taker(2, 2), Taker(3, 2)], available);

        Assert.That(shares.Sum(share => share.UnitsTakenRaw), Is.EqualTo(available));
    }

    #endregion
}
