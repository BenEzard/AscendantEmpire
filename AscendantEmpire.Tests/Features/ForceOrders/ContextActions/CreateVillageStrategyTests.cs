using AscendantEmpire.Application.Features.ForceOrders.ContextActions;
using AscendantEmpire.Application.Features.ForceOrders.ContextActions.Strategies;
using AscendantEmpire.Domain.Enums.Forces;
using AscendantEmpire.Domain.Enums.Maps;

namespace AscendantEmpire.Tests.Features.ForceOrders.ContextActions;

/// <summary>
/// Pure unit tests for <see cref="CreateVillageStrategy"/> — no database required, because the strategy reads only the context handed to it.
/// </summary>
/// <remarks>
/// The numbers mirror the ticket: a settler carrying the <c>found-settlement</c> capability, needing 100 troops — four of its organisation's
/// 25-strong base groups — and 1.0 campaign movement point (1,000 raw, on the Scale 1,000 fixed-point convention).
/// </remarks>
[TestFixture]
internal class CreateVillageStrategyTests
{
    private const string _CAPABILITY_TAG_CODE = "create-settlement";
    private const int _MINIMUM_TROOPS = 100;
    private const long _FULL_CAMPAIGN_MOVEMENT = 1_000;

    private CreateVillageStrategy _strategy = null!;

    [SetUp]
    public void SetUp()
    {
        _strategy = new CreateVillageStrategy();
    }

    /// <summary>Builds the selected Force, defaulting to a settler on open ground that can found a village right now.</summary>
    private static ForceActionForce Force(
        int healthyCount = _MINIMUM_TROOPS,
        long campaignMovementRemaining = _FULL_CAMPAIGN_MOVEMENT,
        bool tileIsWater = false,
        CampaignMapGeographyType tileGeographyType = CampaignMapGeographyType.Flat,
        params string[] capabilityTagCodes)
    {
        return new ForceActionForce
        {
            UnitInstanceId = 5,
            PlayerId = 1,
            CampaignMapTileId = 112,
            DisplayName = "Settler",
            HealthyCount = healthyCount,
            InjuredCount = 0,
            CampaignMovementRemaining = campaignMovementRemaining,
            CampaignMovementPoints = _FULL_CAMPAIGN_MOVEMENT,
            Organisation = null,
            CapabilityTagCodes = capabilityTagCodes.Length > 0 ? capabilityTagCodes : [_CAPABILITY_TAG_CODE],
            TileIsWater = tileIsWater,
            TileGeographyType = tileGeographyType
        };
    }

    private static ForceActionContext Context(ForceActionForce force)
    {
        return new ForceActionContext { SelectedForce = force, OtherForcesOnTile = [] };
    }

    [Test, Description("The action type doubles as display order, so a wrong one would move the button as well as mislabel it.")]
    public void ShouldIdentifyItselfAsTheCreateVillageAction()
    {
        using (Assert.EnterMultipleScope())
        {
            Assert.That(_strategy.ActionType, Is.EqualTo(ForceActionType.CreateVillage));
            Assert.That(_strategy.Label, Is.EqualTo("Create Village"));
        }
    }

    [Test, Description("Carrying camping gear is fixed at the point a Force is fielded, so a Force without it is hidden the action rather than shown a button it can never press.")]
    public void ShouldHideTheActionWhenTheForceLacksTheCapability()
    {
        ForceActionAvailability? result = _strategy.Evaluate(Context(Force(capabilityTagCodes: ["scouting"])));

        Assert.That(result, Is.Null);
    }

    [Test, Description("The two settlement actions carry separate capabilities so equipment can grant one without the other; camping gear alone must not unlock a village.")]
    public void ShouldHideTheActionWhenTheForceOnlyHasTheCampCapability()
    {
        ForceActionAvailability? result = _strategy.Evaluate(Context(Force(capabilityTagCodes: ["found-camp"])));

        Assert.That(result, Is.Null);
    }

    [Test, Description("A capable settler on open ground with troops and movement to spare is the case the whole action exists for.")]
    public void ShouldEnableTheActionWhenEveryConditionIsMet()
    {
        ForceActionAvailability? result = _strategy.Evaluate(Context(Force()));

        Assert.That(result, Is.Not.Null);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(result!.IsEnabled, Is.True);
            Assert.That(result.DisabledReasons, Is.Empty);
        }
    }

    [Test, Description("Terrain is something the player can walk out of, so an unsuitable tile disables the action with an explanation instead of hiding it.")]
    public void ShouldDisableTheActionWhenTheTileIsWater()
    {
        ForceActionAvailability? result = _strategy.Evaluate(Context(Force(tileIsWater: true)));

        Assert.That(result, Is.Not.Null);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(result!.IsEnabled, Is.False);
            Assert.That(result.DisabledReasons, Does.Contain("Settler is on water, and a village must be founded on land."));
        }
    }

    [Test, Description("A village needs four of the settler organisation's base groups, so 99 troops is one short and must say so rather than failing silently.")]
    public void ShouldDisableTheActionWhenTheForceIsBelowTheVillageThreshold()
    {
        ForceActionAvailability? result = _strategy.Evaluate(Context(Force(healthyCount: _MINIMUM_TROOPS - 1)));

        Assert.That(result, Is.Not.Null);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(result!.IsEnabled, Is.False);
            Assert.That(result.DisabledReasons, Does.Contain("Settler has 99 troops, and a village needs at least 100."));
        }
    }

    [Test, Description("The village threshold is four times the camp's; a Force big enough to camp but not to settle must still be refused the village.")]
    public void ShouldDisableTheActionWhenTheForceIsOnlyLargeEnoughForACamp()
    {
        ForceActionAvailability? result = _strategy.Evaluate(Context(Force(healthyCount: 25)));

        Assert.That(result, Is.Not.Null);
        Assert.That(result!.IsEnabled, Is.False);
    }

    [Test, Description("Founding costs movement, so a Force that has already spent its allowance is told to wait rather than being allowed a free action.")]
    public void ShouldDisableTheActionWhenNoMovementRemains()
    {
        ForceActionAvailability? result = _strategy.Evaluate(Context(Force(campaignMovementRemaining: 0)));

        Assert.That(result, Is.Not.Null);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(result!.IsEnabled, Is.False);
            Assert.That(result.DisabledReasons, Does.Contain("Settler has no campaign movement left this turn."));
        }
    }

    [Test, Description("A Force failing several conditions is told all of them at once, so fixing one does not merely reveal the next.")]
    public void ShouldReportEveryFailingConditionWhenSeveralApply()
    {
        ForceActionAvailability? result = _strategy.Evaluate(Context(Force(
            healthyCount: 10,
            campaignMovementRemaining: 0,
            tileIsWater: true)));

        Assert.That(result, Is.Not.Null);
        Assert.That(result!.DisabledReasons, Has.Count.EqualTo(3));
    }
}
