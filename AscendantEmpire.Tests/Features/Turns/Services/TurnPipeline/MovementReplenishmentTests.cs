using AscendantEmpire.Application.Features.Turns.Services.TurnPipeline.Campaign.StartTurn;

namespace AscendantEmpire.Tests.Features.Turns.Services.TurnPipeline;

/// <summary>
/// Pins the start-of-Turn campaign movement rule from <c>docs/design/units/unit-mechanics.md</c>, rules 3 and 4.
/// </summary>
/// <remarks>
/// No database at all. The rule is a pure function of the template's allowance and what the Force is still holding, which is exactly why it was pulled
/// out of the pipeline process — the whole turn-by-turn table can be walked here without standing up a context.
/// </remarks>
[TestFixture]
public class MovementReplenishmentTests
{
    /// <summary>A 2.2 MP allowance: two whole points, with a fractional remainder that is the whole point of these tests.</summary>
    private const long _CAVALRY_ALLOWANCE = 2_200;

    /// <summary>One whole Movement Point, raw.</summary>
    private const long _ONE_POINT = 1_000;

    [Test, Description("A Cavalry on 2.2 spending two whole tiles a Turn accrues the leftover until the fifth Turn, when it can afford a third move and starts over — the worked table in unit-mechanics.md.")]
    public void ShouldAccrueTheFractionalRemainderUntilItBuysAnExtraMove()
    {
        // Turn 1 opens with nothing carried over.
        long remaining = MovementReplenishment.ReplenishCampaignMovement(_CAVALRY_ALLOWANCE, 0);

        var granted = new List<long>();

        for (int turn = 1; turn <= 5; turn++)
        {
            granted.Add(remaining);

            // The Force spends every whole point it can reach this Turn, which is what the design's worked example assumes.
            long wholePointsSpent = remaining / _ONE_POINT * _ONE_POINT;
            remaining = MovementReplenishment.ReplenishCampaignMovement(_CAVALRY_ALLOWANCE, remaining - wholePointsSpent);
        }

        Assert.That(granted, Is.EqualTo(new long[] { 2_200, 2_400, 2_600, 2_800, 3_000 }),
            "Turns 1-4 buy two moves and bank the remainder; Turn 5 reaches a full three points.");
    }

    [Test, Description("A Force that skipped its move resets to the plain allowance and banks nothing, so sitting still cannot compound movement Turn on Turn.")]
    public void ShouldBankNothingWhenTheForceDidNotMove()
    {
        long remaining = MovementReplenishment.ReplenishCampaignMovement(_CAVALRY_ALLOWANCE, _CAVALRY_ALLOWANCE);

        Assert.That(remaining, Is.EqualTo(_CAVALRY_ALLOWANCE));
    }

    [Test, Description("A leftover above the allowance also banks nothing, so a carry the Force never spent cannot roll up on itself.")]
    public void ShouldBankNothingWhenTheLeftoverExceedsTheAllowance()
    {
        long remaining = MovementReplenishment.ReplenishCampaignMovement(_CAVALRY_ALLOWANCE, 3_200);

        Assert.That(remaining, Is.EqualTo(_CAVALRY_ALLOWANCE));
    }

    [Test, Description("Carry is capped at one whole Movement Point, so a Force stuck on 1.5 because it could not afford its next tile opens on 3.2 rather than 3.7.")]
    public void ShouldCapTheCarryAtOneWholeMovementPoint()
    {
        long remaining = MovementReplenishment.ReplenishCampaignMovement(_CAVALRY_ALLOWANCE, 1_500);

        Assert.That(remaining, Is.EqualTo(_CAVALRY_ALLOWANCE + _ONE_POINT));
    }

    [Test, Description("Exactly one point of leftover carries whole, pinning the boundary between the capped and uncapped branches so a later refactor cannot quietly slide it.")]
    public void ShouldCarryExactlyOnePointWhole()
    {
        long remaining = MovementReplenishment.ReplenishCampaignMovement(_CAVALRY_ALLOWANCE, _ONE_POINT);

        Assert.That(remaining, Is.EqualTo(_CAVALRY_ALLOWANCE + _ONE_POINT));
    }

    [Test, Description("Movement debt carries in full and uncapped, so a Force that overspent pays it back out of the next Turn instead of having it forgiven.")]
    public void ShouldCarryDebtInFull()
    {
        long remaining = MovementReplenishment.ReplenishCampaignMovement(_CAVALRY_ALLOWANCE, -1_000);

        Assert.That(remaining, Is.EqualTo(1_200));
    }

    [Test, Description("Debt deeper than a whole allowance leaves the Force short rather than clearing, and recovers over the Turns that follow.")]
    public void ShouldRecoverFromDebtDeeperThanTheAllowanceOverSeveralTurns()
    {
        long afterFirstTurn = MovementReplenishment.ReplenishCampaignMovement(_CAVALRY_ALLOWANCE, -3_000);
        long afterSecondTurn = MovementReplenishment.ReplenishCampaignMovement(_CAVALRY_ALLOWANCE, afterFirstTurn);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(afterFirstTurn, Is.EqualTo(-800), "Still in debt: 2.2 granted against 3.0 owed.");
            Assert.That(afterSecondTurn, Is.EqualTo(1_400), "The remaining 0.8 of debt comes off the next allowance.");
        }
    }

    [Test, Description("A template compiled to no campaign movement grants none, rather than handing over a bare carry.")]
    public void ShouldGrantNothingWhenTheTemplateHasNoAllowance()
    {
        long remaining = MovementReplenishment.ReplenishCampaignMovement(0, 0);

        Assert.That(remaining, Is.Zero);
    }
}
