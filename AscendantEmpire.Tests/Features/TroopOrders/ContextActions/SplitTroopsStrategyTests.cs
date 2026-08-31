using AscendantEmpire.Application.Features.TroopOrders.ContextActions;
using AscendantEmpire.Application.Features.TroopOrders.ContextActions.Strategies;
using AscendantEmpire.Domain.Enums.Troops;
using AscendantEmpire.Domain.Enums.UnitDesigns;

namespace AscendantEmpire.Tests.Features.TroopOrders.ContextActions;

/// <summary>
/// Pure unit tests for <see cref="SplitTroopsStrategy"/> — no database required, because the strategy reads only the context handed to it.
/// </summary>
/// <remarks>
/// The numbers mirror the settler in the ticket: an unled organisation with a base size of 25, and a template allowing 1.0 campaign movement
/// point (1,000 raw, on the Scale 1,000 fixed-point convention).
/// </remarks>
[TestFixture]
internal class SplitTroopsStrategyTests
{
    private const int _UNLED_BASE_SIZE = 25;
    private const int _UNLED_MAX_SIZE = 200;
    private const long _FULL_CAMPAIGN_MOVEMENT = 1_000;

    private SplitTroopsStrategy _strategy = null!;

    [SetUp]
    public void SetUp()
    {
        _strategy = new SplitTroopsStrategy();
    }

    /// <summary>Builds an organisation tag; defaults to the unled settler shape from the ticket.</summary>
    private static TroopActionOrganisation Organisation(
        LeadershipModel? leadershipModel = LeadershipModel.Unled,
        int? unledBaseSize = _UNLED_BASE_SIZE)
    {
        return new TroopActionOrganisation
        {
            TagId = 9,
            Code = "settler",
            DisplayName = "Settler",
            LeadershipModel = leadershipModel,
            UnledBaseSize = unledBaseSize,
            UnledMaxSize = _UNLED_MAX_SIZE
        };
    }

    /// <summary>Builds the selected troop, defaulting to a 100-strong settler that has not yet moved.</summary>
    private static TroopActionTroop Troop(
        int healthyCount = 100,
        int injuredCount = 0,
        long movementPointsRemaining = _FULL_CAMPAIGN_MOVEMENT,
        TroopActionOrganisation? organisation = null)
    {
        return new TroopActionTroop
        {
            TroopInstanceId = 5,
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

    private static TroopActionContext Context(TroopActionTroop troop)
    {
        return new TroopActionContext { SelectedTroop = troop, OtherTroopsOnTile = [] };
    }

    [Test, Description("The strategy answers for the SplitTroops action, which is also what fixes its position in the toolbar — the enum value doubles "
        + "as the display order, so an incorrect action type would silently move the button as well as mislabel it.")]
    public void ShouldIdentifyItselfAsTheSplitTroopsAction()
    {
        Assert.That(_strategy.ActionType, Is.EqualTo(TroopActionType.SplitTroops));
    }

    [Test, Description("A template carrying no organisation tag has no unled base size to divide along, so there is no rule by which it could ever "
        + "split. The action is hidden rather than disabled, because nothing the player does this turn could make it apply.")]
    public void ShouldHideTheActionWhenTheTroopHasNoOrganisationTag()
    {
        // The builder defaults to a settler organisation, so the tagless case is expressed by stripping it back off.
        TroopActionAvailability? result = _strategy.Evaluate(Context(Troop() with { Organisation = null }));

        Assert.That(result, Is.Null, "A troop with no organisation tag should not be offered the split action at all.");
    }

    [Test, Description("Commissioned formations detach a sub-formation under a subordinate officer instead, which is a different action with different "
        + "rules. Offering this one greyed out would imply the troop could eventually use it, which is never true.")]
    public void ShouldHideTheActionWhenTheOrganisationIsCommissioned()
    {
        TroopActionAvailability? result = _strategy.Evaluate(Context(Troop(organisation: Organisation(LeadershipModel.Commissioned))));

        Assert.That(result, Is.Null, "A commissioned troop should not be offered the unled split action.");
    }

    [Test, Description("An unspecified leadership model is treated as commissioned everywhere else in the system, so the split action must read it the "
        + "same way rather than treating the absence of a value as permission.")]
    public void ShouldHideTheActionWhenTheLeadershipModelIsUnspecified()
    {
        TroopActionAvailability? result = _strategy.Evaluate(Context(Troop(organisation: Organisation(leadershipModel: null))));

        Assert.That(result, Is.Null, "An unspecified leadership model should be treated as commissioned, and so hidden.");
    }

    [Test, Description("The ordinary success case: an unled troop big enough to yield two whole groups, with its full turn still ahead of it, has "
        + "nothing standing in its way and must come back enabled with no reasons attached.")]
    public void ShouldAllowTheSplitWhenTheTroopIsUnledLargeEnoughAndUnmoved()
    {
        TroopActionAvailability? result = _strategy.Evaluate(Context(Troop()));

        Assert.That(result, Is.Not.Null);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(result!.IsEnabled, Is.True, "A full-strength, unmoved unled troop should be splittable.");
            Assert.That(result.DisabledReasons, Is.Empty);
        }
    }

    [Test, Description("Two whole groups is the floor, not something to exceed. A troop of exactly twice the base size can divide cleanly in two, so "
        + "the boundary must be inclusive — an off-by-one here would block the most common split there is.")]
    public void ShouldAllowTheSplitWhenTheTroopIsExactlyTwoWholeGroups()
    {
        TroopActionAvailability? result = _strategy.Evaluate(Context(Troop(healthyCount: _UNLED_BASE_SIZE * 2)));

        Assert.That(result!.IsEnabled, Is.True, "A troop of exactly two whole groups is at the boundary and should be splittable.");
    }

    [Test, Description("Injured soldiers are still with the troop and still move with it, so they count towards the size a split is measured against. "
        + "Counting only the healthy would wrongly block a battered troop that is plainly large enough to divide.")]
    public void ShouldAllowTheSplitWhenHealthyAndInjuredTogetherMakeTwoGroups()
    {
        TroopActionAvailability? result = _strategy.Evaluate(Context(Troop(healthyCount: _UNLED_BASE_SIZE, injuredCount: _UNLED_BASE_SIZE)));

        Assert.That(result!.IsEnabled, Is.True, "Healthy and injured together make two whole groups, so the troop should be splittable.");
    }

    [Test, Description("A troop that cannot yield two whole groups has nothing to divide into. This is a state the player can change by reinforcing, "
        + "so the action is disabled with an explanation rather than hidden.")]
    public void ShouldBlockTheSplitWhenTheTroopCannotYieldTwoWholeGroups()
    {
        TroopActionAvailability? result = _strategy.Evaluate(Context(Troop(healthyCount: (_UNLED_BASE_SIZE * 2) - 1)));

        Assert.That(result, Is.Not.Null);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(result!.IsEnabled, Is.False);
            Assert.That(result.DisabledReasons, Has.Exactly(1).Items);
            Assert.That(result.DisabledReasons.Single(), Does.Contain("too small to split").And.Contain("Settler"),
                "The reason should name the troop it is about, so it still reads correctly with several troops on a tile.");
        }
    }

    [Test, Description("Splitting costs the troop its whole turn, so it needs a turn that has not been started — not merely one with movement left. A "
        + "troop one point down has already gone somewhere, and must be refused.")]
    public void ShouldBlockTheSplitWhenAnyMovementHasAlreadyBeenSpent()
    {
        TroopActionAvailability? result = _strategy.Evaluate(Context(Troop(movementPointsRemaining: _FULL_CAMPAIGN_MOVEMENT - 1)));

        Assert.That(result, Is.Not.Null);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(result!.IsEnabled, Is.False);
            Assert.That(result.DisabledReasons, Has.Exactly(1).Items);
            Assert.That(result.DisabledReasons.Single(), Does.Contain("already moved this turn"));
        }
    }

    [Test, Description("Every failing rule is reported at once, so a player who fixes the one problem they were told about doesn't discover a second. "
        + "Stopping at the first failure would make the toolbar feel like it was moving the goalposts.")]
    public void ShouldReportEveryFailingRuleWhenBothSizeAndMovementFail()
    {
        TroopActionAvailability? result = _strategy.Evaluate(Context(Troop(healthyCount: 10, movementPointsRemaining: 0)));

        Assert.That(result, Is.Not.Null);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(result!.IsEnabled, Is.False);
            Assert.That(result.DisabledReasons, Has.Exactly(2).Items, "Both broken rules should be reported at once.");
            Assert.That(result.DisabledReasons, Has.One.Contains("too small to split"));
            Assert.That(result.DisabledReasons, Has.One.Contains("already moved this turn"));
        }
    }

    [Test, Description("An unled tag is authored with its base and max sizes together, so one lacking them is a configuration error. Surfacing it beats "
        + "hiding the action as though the troop were commissioned, which would disguise bad data as a design decision.")]
    public void ShouldBlockTheSplitWhenAnUnledTagHasNoAuthoredBaseSize()
    {
        TroopActionAvailability? result = _strategy.Evaluate(Context(Troop(organisation: Organisation(unledBaseSize: null))));

        Assert.That(result, Is.Not.Null, "A misconfigured unled tag should be reported, not hidden as though it were commissioned.");

        using (Assert.EnterMultipleScope())
        {
            Assert.That(result!.IsEnabled, Is.False);
            Assert.That(result.DisabledReasons.Single(), Does.Contain("no authored base size"));
        }
    }
}
