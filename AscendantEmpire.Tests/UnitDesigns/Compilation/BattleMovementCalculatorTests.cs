using AscendantEmpire.Application.Features.UnitComponents.Services;

namespace AscendantEmpire.Tests.UnitDesigns.Compilation;

/// <summary>
/// Pure unit tests for <see cref="BattleMovementCalculator"/> — no database required.
/// </summary>
/// <remarks>
/// <para>Every value is raw Scale-1,000 fixed-point (1,000 raw = 1.0), the form the calculator works in.</para>
/// <para>The named constants below are the agreed calibration from UD-52. They are what pins the formula to the intended feel of the game, so
/// the loadout tests read as the design targets they came from rather than as bare arithmetic.</para>
/// </remarks>
[TestFixture]
internal class BattleMovementCalculatorTests
{
    /// <summary>Power of the human body: 7.0.</summary>
    private const long HUMAN_BODY_POWER = 7_000;

    /// <summary>Weight of a spear or a sword: 1.0. The two weigh the same.</summary>
    private const long SPEAR_WEIGHT = 1_000;

    /// <summary>Weight of a knife: 0.5, half a spear.</summary>
    private const long KNIFE_WEIGHT = 500;

    /// <summary>Weight of a suit of armour: 0.5.</summary>
    private const long ARMOUR_WEIGHT = 500;

    /// <summary>Weight of a shield: 0.5.</summary>
    private const long SHIELD_WEIGHT = 500;

    [Test, Description("A man carrying only a sword or a spear moves 6 tiles.")]
    public void SingleHandWeaponLeavesSixTiles()
    {
        long movementPoints = BattleMovementCalculator.CalculateBattlefieldMovementPoints(HUMAN_BODY_POWER, SPEAR_WEIGHT);

        Assert.That(movementPoints, Is.EqualTo(6_000), "7.0 power carrying a 1.0 spear should leave 6.0 movement points.");
    }

    [Test, Description("A man carrying only a knife moves 6.5 tiles — the half-tile case the fixed-point scale has to survive.")]
    public void KnifeLeavesSixAndAHalfTiles()
    {
        long movementPoints = BattleMovementCalculator.CalculateBattlefieldMovementPoints(HUMAN_BODY_POWER, KNIFE_WEIGHT);

        Assert.That(movementPoints, Is.EqualTo(6_500), "7.0 power carrying a 0.5 knife should leave 6.5 movement points.");
    }

    [Test, Description("A man carrying armour, a spear and a shield moves 5 tiles.")]
    public void FullLoadoutLeavesFiveTiles()
    {
        long carriedWeight = ARMOUR_WEIGHT + SPEAR_WEIGHT + SHIELD_WEIGHT;

        long movementPoints = BattleMovementCalculator.CalculateBattlefieldMovementPoints(HUMAN_BODY_POWER, carriedWeight);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(carriedWeight, Is.EqualTo(2_000), "Armour, spear and shield should come to 2.0 of carried weight.");
            Assert.That(movementPoints, Is.EqualTo(5_000), "7.0 power carrying 2.0 of kit should leave 5.0 movement points.");
        }
    }

    [Test, Description("Carrying exactly as much as the body can power leaves no movement at all, but not less.")]
    public void CarryingExactlyPowerLeavesZero()
    {
        long movementPoints = BattleMovementCalculator.CalculateBattlefieldMovementPoints(HUMAN_BODY_POWER, HUMAN_BODY_POWER);

        Assert.That(movementPoints, Is.Zero, "Weight equal to power should leave exactly zero movement points.");
    }

    [Test, Description("An over-loaded design is floored at zero rather than going negative.")]
    public void OverLoadedDesignIsFlooredAtZero()
    {
        // Twice as much kit as the body can power — comfortably past the floor rather than just over it.
        long movementPoints = BattleMovementCalculator.CalculateBattlefieldMovementPoints(HUMAN_BODY_POWER, HUMAN_BODY_POWER * 2);

        Assert.That(movementPoints, Is.EqualTo(BattleMovementCalculator.MINIMUM_BATTLEFIELD_MOVEMENT_POINTS_RAW),
            "An over-loaded design should be pinned to the floor, not given negative movement.");
    }

    [Test, Description("A body carrying nothing keeps its full power as movement.")]
    public void EmptyLoadoutKeepsFullPower()
    {
        long movementPoints = BattleMovementCalculator.CalculateBattlefieldMovementPoints(HUMAN_BODY_POWER, 0);

        Assert.That(movementPoints, Is.EqualTo(HUMAN_BODY_POWER), "With nothing carried, movement should equal the body's power.");
    }
}
