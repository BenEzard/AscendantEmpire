using AscendantEmpire.Application.Features.ForceOrders.ContextActions;
using AscendantEmpire.Application.Features.ForceOrders.ContextActions.Strategies;
using AscendantEmpire.Domain.Enums.Forces;
using AscendantEmpire.Domain.Enums.Maps;

namespace AscendantEmpire.Tests.Features.ForceOrders.ContextActions;

/// <summary>
/// Pure unit tests for <see cref="EstablishCampStrategy"/> — no database required, because the strategy reads only the context handed to it.
/// </summary>
/// <remarks>
/// The numbers mirror the ticket: a settler carrying the <c>found-camp</c> capability, needing 25 troops — one of its organisation's base groups —
/// and 1.0 campaign movement point (1,000 raw, on the Scale 1,000 fixed-point convention).
/// </remarks>
[TestFixture]
internal class EstablishCampStrategyTests
{
    private const string _CAPABILITY_TAG_CODE = "create-early-camp";
    private const int _MINIMUM_TROOPS = 25;
    private const long _FULL_CAMPAIGN_MOVEMENT = 1_000;

    private EstablishCampStrategy _strategy = null!;

    [SetUp]
    public void SetUp()
    {
        _strategy = new EstablishCampStrategy();
    }

    /// <summary>Builds the selected Force, defaulting to a settler on open ground that can establish a camp right now.</summary>
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
    public void ShouldIdentifyItselfAsTheEstablishCampAction()
    {
        using (Assert.EnterMultipleScope())
        {
            Assert.That(_strategy.ActionType, Is.EqualTo(ForceActionType.EstablishCamp));
            Assert.That(_strategy.Label, Is.EqualTo("Establish Camp"));
        }
    }

    [Test, Description("Carrying camping gear is fixed at the point a Force is fielded, so a Force without it is hidden the action rather than shown a button it can never press.")]
    public void ShouldHideTheActionWhenTheForceLacksTheCapability()
    {
        ForceActionAvailability? result = _strategy.Evaluate(Context(Force(capabilityTagCodes: ["scouting"])));

        Assert.That(result, Is.Null);
    }

    [Test, Description("The two settlement actions carry separate capabilities, so equipment granting only settlement must not also unlock camping.")]
    public void ShouldHideTheActionWhenTheForceOnlyHasTheSettlementCapability()
    {
        ForceActionAvailability? result = _strategy.Evaluate(Context(Force(capabilityTagCodes: ["found-settlement"])));

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

    [Test, Description("A camp needs a quarter of what a village does, so a 25-strong Force that could never settle must still be able to camp.")]
    public void ShouldEnableTheActionWhenTheForceIsTooSmallToFoundAVillage()
    {
        ForceActionAvailability? result = _strategy.Evaluate(Context(Force(healthyCount: _MINIMUM_TROOPS)));

        Assert.That(result, Is.Not.Null);
        Assert.That(result!.IsEnabled, Is.True);
    }

    [Test, Description("Terrain is something the player can walk out of, so an unsuitable tile disables the action with an explanation instead of hiding it.")]
    public void ShouldDisableTheActionWhenTheTileIsWater()
    {
        ForceActionAvailability? result = _strategy.Evaluate(Context(Force(tileIsWater: true)));

        Assert.That(result, Is.Not.Null);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(result!.IsEnabled, Is.False);
            Assert.That(result.DisabledReasons, Does.Contain("Settler is on water, and a camp must be founded on land."));
        }
    }

    [Test, Description("Mountains cannot be settled, and the reason must name the terrain so the player knows to move rather than to reinforce.")]
    public void ShouldDisableTheActionWhenTheTileIsMountainous()
    {
        ForceActionAvailability? result = _strategy.Evaluate(Context(Force(tileGeographyType: CampaignMapGeographyType.Mountainous)));

        Assert.That(result, Is.Not.Null);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(result!.IsEnabled, Is.False);
            Assert.That(result.DisabledReasons, Does.Contain("Settler is in mountainous terrain, which cannot be settled."));
        }
    }

    [Test, Description("Below one base group there is no camp to make, so 24 troops is refused with both numbers named.")]
    public void ShouldDisableTheActionWhenTheForceIsBelowTheCampThreshold()
    {
        ForceActionAvailability? result = _strategy.Evaluate(Context(Force(healthyCount: _MINIMUM_TROOPS - 1)));

        Assert.That(result, Is.Not.Null);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(result!.IsEnabled, Is.False);
            Assert.That(result.DisabledReasons, Does.Contain("Settler has 24 troops, and a camp needs at least 25."));
        }
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
            healthyCount: 1,
            campaignMovementRemaining: 0,
            tileIsWater: true)));

        Assert.That(result, Is.Not.Null);
        Assert.That(result!.DisabledReasons, Has.Count.EqualTo(3));
    }
}
