using AscendantEmpire.Application.Features.ForceOrders.ContextActions;
using AscendantEmpire.Application.Features.ForceOrders.ContextActions.Strategies;
using AscendantEmpire.Application.Features.Maps.MapResources.Classes;
using AscendantEmpire.Domain.Enums.Forces;

namespace AscendantEmpire.Tests.Features.ForceOrders.ContextActions;

/// <summary>
/// Pure unit tests for <see cref="TakeResourcesStrategy"/> — no database required, because the strategy reads only the context handed to it.
/// </summary>
/// <remarks>
/// What makes a MapResource takeable at all is settled before the strategy sees it, so nothing here authors a quantity or a switch. These pin the two
/// structural cases that hide the button and the two conditions that grey it out.
/// </remarks>
[TestFixture]
internal class TakeResourcesStrategyTests
{
    private const long _FULL_CAMPAIGN_MOVEMENT = 1_000;

    private TakeResourcesStrategy _strategy = null!;

    [SetUp]
    public void SetUp()
    {
        _strategy = new TakeResourcesStrategy();
    }

    #region Helpers

    /// <summary>Builds the selected Force, defaulting to a healthy one that has not yet moved.</summary>
    private static ForceActionForce Force(
        int healthyCount = 20,
        long campaignMovementRemaining = _FULL_CAMPAIGN_MOVEMENT,
        bool isInBattle = false)
    {
        return new ForceActionForce
        {
            UnitInstanceId = 5,
            PlayerId = 1,
            CampaignMapTileId = 112,
            DisplayName = "Warrior",
            HealthyCount = healthyCount,
            InjuredCount = 0,
            CampaignMovementRemaining = campaignMovementRemaining,
            CampaignMovementPoints = _FULL_CAMPAIGN_MOVEMENT,
            Organisation = null,
            // Taking reads none of these; they are here because the record requires them.
            CapabilityTagCodes = [],
            TileIsWater = false,
            TileGeographyIsTraversable = true,
            TileGeographyName = "Flat",
            IsInBattle = isInBattle
        };
    }

    /// <summary>A takeable berry patch, standing in for whatever the reader found on the tile.</summary>
    private static TakeableMapResource Berries()
    {
        return new TakeableMapResource(
            MapResourceInstanceId: 41,
            MapResourceTypeId: 3,
            Slot: 1,
            DisplayName: "Berries",
            IconPath: "/map-resources/berries.png",
            QuantityRaw: 10_000_000,
            Yields: [new TakeableMapResourceYield(AssetId: 7, AssetDisplayName: "Berries", AmountPerUnit: 1_000)]);
    }

    private static ForceActionContext Context(ForceActionForce force, params TakeableMapResource[] takeable)
    {
        return new ForceActionContext { SelectedForce = force, OtherForcesOnTile = [], TakeableMapResourcesOnTile = takeable };
    }

    #endregion

    #region Identity

    [Test, Description("The action type doubles as display order, so a wrong one would move the button as well as mislabel it.")]
    public void ShouldIdentifyItselfAsTheTakeResourcesAction()
    {
        using (Assert.EnterMultipleScope())
        {
            Assert.That(_strategy.ActionType, Is.EqualTo(ForceActionType.TakeResources));
            Assert.That(_strategy.Label, Is.EqualTo("Take Resources"));
        }
    }

    #endregion

    #region Hidden

    [Test, Description("Most tiles hold nothing takeable, so the button is hidden rather than greyed — a disabled button on every empty tile is the wall the three-state return exists to avoid.")]
    public void ShouldHideTheActionWhenTheTileHoldsNothingTakeable()
    {
        ForceActionAvailability? result = _strategy.Evaluate(Context(Force()));

        Assert.That(result, Is.Null);
    }

    [Test, Description("A Force fighting a battle is not foraging through it, and nothing it can do this turn changes that, so it is hidden the button.")]
    public void ShouldHideTheActionWhenTheForceIsInBattle()
    {
        ForceActionAvailability? result = _strategy.Evaluate(Context(Force(isInBattle: true), Berries()));

        Assert.That(result, Is.Null);
    }

    #endregion

    #region Offered

    [Test, Description("The ordinary success case: a healthy, unmoved Force on a tile with something takeable comes back enabled, no reasons.")]
    public void ShouldOfferTheActionToAHealthyUnmovedForce()
    {
        ForceActionAvailability? result = _strategy.Evaluate(Context(Force(), Berries()));

        Assert.That(result, Is.Not.Null);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(result!.IsEnabled, Is.True);
            Assert.That(result.DisabledReasons, Is.Empty);
        }
    }

    [Test, Description("Movement carried over from an earlier turn leaves a Force above its allowance, which still reads as a full turn — the gate cannot tell carry-over from an unmoved Force, and must not refuse both.")]
    public void ShouldOfferTheActionToAForceCarryingMoreThanAFullAllowance()
    {
        ForceActionAvailability? result = _strategy.Evaluate(Context(Force(campaignMovementRemaining: _FULL_CAMPAIGN_MOVEMENT + 500), Berries()));

        Assert.That(result, Is.Not.Null);
        Assert.That(result!.IsEnabled, Is.True);
    }

    #endregion

    #region Blocked

    [Test, Description("The wounded carry nothing, so a Force of nothing but casualties is told why rather than quietly taking zero.")]
    public void ShouldBlockTheActionWhenNoIndividualIsHealthy()
    {
        ForceActionAvailability? result = _strategy.Evaluate(Context(Force(healthyCount: 0), Berries()));

        Assert.That(result, Is.Not.Null);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(result!.IsEnabled, Is.False);
            Assert.That(result.DisabledReasons, Has.Exactly(1).Items);
            Assert.That(result.DisabledReasons.Single(), Does.Contain("no healthy individuals").And.Contain("Warrior"),
                "The reason should name the Force it is about, so it still reads correctly with several Forces selected.");
        }
    }

    [Test, Description("Loading up costs a whole turn, so a Force even one movement point down has already gone somewhere this turn.")]
    public void ShouldBlockTheActionWhenAnyMovementHasAlreadyBeenSpent()
    {
        ForceActionAvailability? result = _strategy.Evaluate(Context(Force(campaignMovementRemaining: _FULL_CAMPAIGN_MOVEMENT - 1), Berries()));

        Assert.That(result, Is.Not.Null);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(result!.IsEnabled, Is.False);
            Assert.That(result.DisabledReasons.Single(), Does.Contain("already moved this turn"));
        }
    }

    [Test, Description("Every failing condition is reported at once, so fixing one problem does not simply reveal the next.")]
    public void ShouldReportEveryFailingConditionTogether()
    {
        ForceActionAvailability? result = _strategy.Evaluate(Context(Force(healthyCount: 0, campaignMovementRemaining: 0), Berries()));

        Assert.That(result, Is.Not.Null);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(result!.IsEnabled, Is.False);
            Assert.That(result.DisabledReasons, Has.Exactly(2).Items);
            Assert.That(result.DisabledReasons, Has.One.Contains("no healthy individuals"));
            Assert.That(result.DisabledReasons, Has.One.Contains("already moved this turn"));
        }
    }

    #endregion
}
