using AscendantEmpire.Application.Features.ForceOrders.ContextActions;
using AscendantEmpire.Application.Features.ForceOrders.ContextActions.Strategies;
using AscendantEmpire.Domain.Enums.Forces;
using AscendantEmpire.Domain.Enums.UnitDesigns;

namespace AscendantEmpire.Tests.Features.ForceOrders.ContextActions;

/// <summary>
/// Pure unit tests for <see cref="SplitForceStrategy"/> — no database required, because the strategy reads only the context handed to it.
/// </summary>
/// <remarks>
/// The numbers mirror the settler in the ticket: an unled organisation with a base size of 25, and a template allowing 1.0 campaign movement
/// point (1,000 raw, on the Scale 1,000 fixed-point convention).
/// </remarks>
[TestFixture]
internal class SplitForceStrategyTests
{
    private const int _UNLED_BASE_SIZE = 25;
    private const int _UNLED_MAX_SIZE = 200;
    private const long _FULL_CAMPAIGN_MOVEMENT = 1_000;

    private SplitForceStrategy _strategy = null!;

    [SetUp]
    public void SetUp()
    {
        _strategy = new SplitForceStrategy();
    }

    /// <summary>Builds an organisation tag; defaults to the unled settler shape from the ticket.</summary>
    private static ForceActionOrganisation Organisation(
        LeadershipModel? leadershipModel = LeadershipModel.Unled,
        int? unledBaseSize = _UNLED_BASE_SIZE)
    {
        return new ForceActionOrganisation
        {
            TagId = 9,
            Code = "settler",
            DisplayName = "Settler",
            LeadershipModel = leadershipModel,
            UnledBaseSize = unledBaseSize,
            UnledMaxSize = _UNLED_MAX_SIZE
        };
    }

    /// <summary>Builds the selected Force, defaulting to a 100-strong settler that has not yet moved.</summary>
    private static ForceActionForce Force(
        int healthyCount = 100,
        int injuredCount = 0,
        long movementPointsRemaining = _FULL_CAMPAIGN_MOVEMENT,
        ForceActionOrganisation? organisation = null)
    {
        return new ForceActionForce
        {
            UnitInstanceId = 5,
            PlayerId = 1,
            CampaignMapTileId = 112,
            DisplayName = "Settler",
            HealthyCount = healthyCount,
            InjuredCount = injuredCount,
            MovementPointsRemaining = movementPointsRemaining,
            CampaignMovementPoints = _FULL_CAMPAIGN_MOVEMENT,
            Organisation = organisation ?? Organisation()
        };
    }

    private static ForceActionContext Context(ForceActionForce force)
    {
        return new ForceActionContext { SelectedForce = force, OtherForcesOnTile = [] };
    }

    [Test, Description("The action type doubles as display order, so a wrong one would move the button as well as mislabel it.")]
    public void ShouldIdentifyItselfAsTheSplitForceAction()
    {
        Assert.That(_strategy.ActionType, Is.EqualTo(ForceActionType.SplitForce));
    }

    [Test, Description("With no organisation tag there is no base size to divide along, so the action is hidden rather than disabled.")]
    public void ShouldHideTheActionWhenTheForceHasNoOrganisationTag()
    {
        // The builder defaults to a settler organisation, so the tagless case is expressed by stripping it back off.
        ForceActionAvailability? result = _strategy.Evaluate(Context(Force() with { Organisation = null }));

        Assert.That(result, Is.Null, "A force with no organisation tag should not be offered the split action at all.");
    }

    [Test, Description("Commissioned formations detach instead, a different action, so this one is hidden rather than greyed out.")]
    public void ShouldHideTheActionWhenTheOrganisationIsCommissioned()
    {
        ForceActionAvailability? result = _strategy.Evaluate(Context(Force(organisation: Organisation(LeadershipModel.Commissioned))));

        Assert.That(result, Is.Null, "A commissioned force should not be offered the unled split action.");
    }

    [Test, Description("An unspecified leadership model reads as commissioned here too, not as permission by absence.")]
    public void ShouldHideTheActionWhenTheLeadershipModelIsUnspecified()
    {
        ForceActionAvailability? result = _strategy.Evaluate(Context(Force(organisation: Organisation(leadershipModel: null))));

        Assert.That(result, Is.Null, "An unspecified leadership model should be treated as commissioned, and so hidden.");
    }

    [Test, Description("The ordinary success case: an unled force of two whole groups with a full turn comes back enabled, no reasons.")]
    public void ShouldAllowTheSplitWhenTheForceIsUnledLargeEnoughAndUnmoved()
    {
        ForceActionAvailability? result = _strategy.Evaluate(Context(Force()));

        Assert.That(result, Is.Not.Null);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(result!.IsEnabled, Is.True, "A full-strength, unmoved unled force should be splittable.");
            Assert.That(result.DisabledReasons, Is.Empty);
        }
    }

    [Test, Description("Two whole groups is the inclusive floor; an off-by-one would block the most common split there is.")]
    public void ShouldAllowTheSplitWhenTheForceIsExactlyTwoWholeGroups()
    {
        ForceActionAvailability? result = _strategy.Evaluate(Context(Force(healthyCount: _UNLED_BASE_SIZE * 2)));

        Assert.That(result!.IsEnabled, Is.True, "A force of exactly two whole groups is at the boundary and should be splittable.");
    }

    [Test, Description("Injured soldiers still move with the force, so they count towards the size a split is measured against.")]
    public void ShouldAllowTheSplitWhenHealthyAndInjuredTogetherMakeTwoGroups()
    {
        ForceActionAvailability? result = _strategy.Evaluate(Context(Force(healthyCount: _UNLED_BASE_SIZE, injuredCount: _UNLED_BASE_SIZE)));

        Assert.That(result!.IsEnabled, Is.True, "Healthy and injured together make two whole groups, so the force should be splittable.");
    }

    [Test, Description("Too small to yield two groups is fixable by reinforcing, so the action is disabled with a reason, not hidden.")]
    public void ShouldBlockTheSplitWhenTheForceCannotYieldTwoWholeGroups()
    {
        ForceActionAvailability? result = _strategy.Evaluate(Context(Force(healthyCount: (_UNLED_BASE_SIZE * 2) - 1)));

        Assert.That(result, Is.Not.Null);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(result!.IsEnabled, Is.False);
            Assert.That(result.DisabledReasons, Has.Exactly(1).Items);
            Assert.That(result.DisabledReasons.Single(), Does.Contain("too small to split").And.Contain("Settler"),
                "The reason should name the force it is about, so it still reads correctly with several forces on a tile.");
        }
    }

    [Test, Description("Splitting costs a whole turn, so a force even one movement point down has already gone somewhere.")]
    public void ShouldBlockTheSplitWhenAnyMovementHasAlreadyBeenSpent()
    {
        ForceActionAvailability? result = _strategy.Evaluate(Context(Force(movementPointsRemaining: _FULL_CAMPAIGN_MOVEMENT - 1)));

        Assert.That(result, Is.Not.Null);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(result!.IsEnabled, Is.False);
            Assert.That(result.DisabledReasons, Has.Exactly(1).Items);
            Assert.That(result.DisabledReasons.Single(), Does.Contain("already moved this turn"));
        }
    }

    [Test, Description("Every failing rule is reported at once, so fixing one problem does not simply reveal the next.")]
    public void ShouldReportEveryFailingRuleWhenBothSizeAndMovementFail()
    {
        ForceActionAvailability? result = _strategy.Evaluate(Context(Force(healthyCount: 10, movementPointsRemaining: 0)));

        Assert.That(result, Is.Not.Null);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(result!.IsEnabled, Is.False);
            Assert.That(result.DisabledReasons, Has.Exactly(2).Items, "Both broken rules should be reported at once.");
            Assert.That(result.DisabledReasons, Has.One.Contains("too small to split"));
            Assert.That(result.DisabledReasons, Has.One.Contains("already moved this turn"));
        }
    }

    [Test, Description("An unled tag missing its sizes is bad data, surfaced rather than disguised as a commissioned force.")]
    public void ShouldBlockTheSplitWhenAnUnledTagHasNoAuthoredBaseSize()
    {
        ForceActionAvailability? result = _strategy.Evaluate(Context(Force(organisation: Organisation(unledBaseSize: null))));

        Assert.That(result, Is.Not.Null, "A misconfigured unled tag should be reported, not hidden as though it were commissioned.");

        using (Assert.EnterMultipleScope())
        {
            Assert.That(result!.IsEnabled, Is.False);
            Assert.That(result.DisabledReasons.Single(), Does.Contain("no authored base size"));
        }
    }
}
