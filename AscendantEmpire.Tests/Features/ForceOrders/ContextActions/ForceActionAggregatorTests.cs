using AscendantEmpire.Application.Features.ForceOrders.ContextActions;
using AscendantEmpire.Domain.Enums.Forces;

namespace AscendantEmpire.Tests.Features.ForceOrders.ContextActions;

/// <summary>
/// Pure unit tests for <see cref="ForceActionAggregator"/> — no database, no strategies, just the fold from many verdicts to one.
/// </summary>
/// <remarks>
/// This is where the multi-select rule lives, so these tests are the specification of it: hidden only when no selected Force could ever take the
/// action, enabled only when every one of them can, and disabled with the collected reasons everywhere in between.
/// </remarks>
[TestFixture]
internal class ForceActionAggregatorTests
{
    private const string _LABEL = "Split Force";

    private static readonly IForceActionStrategy _STRATEGY = new LabelOnlyStrategy();

    /// <summary>Pairs a verdict with a context whose Force is named for readable reason text.</summary>
    private static (ForceActionContext Context, ForceActionAvailability? Verdict) Verdict(
        int forceId,
        string displayName,
        ForceActionAvailability? availability)
    {
        var context = new ForceActionContext
        {
            SelectedForce = new ForceActionForce
            {
                UnitInstanceId = forceId,
                PlayerId = 1,
                CampaignMapTileId = 112,
                DisplayName = displayName,
                HealthyCount = 100,
                InjuredCount = 0,
                CampaignMovementRemaining = 1_000,
                CampaignMovementPoints = 1_000,
                Organisation = null,
                CapabilityTagCodes = [],
                TileIsWater = false,
                TileGeographyIsTraversable = true,
                TileGeographyName = "Flat",
                IsInBattle = false
            },
            OtherForcesOnTile = []
        };

        return (context, availability);
    }

    [Test, Description("An action no selected Force could ever take is hidden, so a mixed selection cannot fill the toolbar with impossible buttons.")]
    public void ShouldHideTheActionWhenEverySelectedForceFindsItStructurallyIrrelevant()
    {
        ForceActionAvailability? result = ForceActionAggregator.Aggregate(_STRATEGY,
        [
            Verdict(1, "Warband", availability: null),
            Verdict(2, "Warband", availability: null)
        ]);

        Assert.That(result, Is.Null);
    }

    [Test, Description("An action every selected Force can take is enabled, since pressing it is one order over the whole selection.")]
    public void ShouldEnableTheActionWhenEverySelectedForceCanTakeIt()
    {
        ForceActionAvailability? result = ForceActionAggregator.Aggregate(_STRATEGY,
        [
            Verdict(1, "Settler", ForceActionAvailability.Available()),
            Verdict(2, "Settler", ForceActionAvailability.Available()),
            Verdict(3, "Settler", ForceActionAvailability.Available())
        ]);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(result, Is.Not.Null);
            Assert.That(result!.IsEnabled, Is.True);
            Assert.That(result.DisabledReasons, Is.Empty);
        }
    }

    [Test, Description("One structurally irrelevant Force among willing ones disables rather than hides, and names the Force that spoiled it.")]
    public void ShouldDisableAndExplainWhenTheActionIsIrrelevantToOnlySomeOfTheSelection()
    {
        ForceActionAvailability? result = ForceActionAggregator.Aggregate(_STRATEGY,
        [
            Verdict(1, "Settler", ForceActionAvailability.Available()),
            Verdict(2, "Warband", availability: null)
        ]);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(result!.IsEnabled, Is.False);
            Assert.That(result.DisabledReasons.Single(), Is.EqualTo("'Split Force' does not apply to Warband."),
                "A button disabled by a Force it cannot apply to must say which Force, or the reasons all name someone else.");
        }
    }

    [Test, Description("One blocked Force disables the action for all of them, carrying that Force's own reasons through unchanged.")]
    public void ShouldDisableTheActionWhenASingleSelectedForceIsBlocked()
    {
        ForceActionAvailability? result = ForceActionAggregator.Aggregate(_STRATEGY,
        [
            Verdict(1, "Settler A", ForceActionAvailability.Available()),
            Verdict(2, "Settler B", ForceActionAvailability.Blocked(["Settler B is too small to split."]))
        ]);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(result!.IsEnabled, Is.False);
            Assert.That(result.DisabledReasons.Single(), Is.EqualTo("Settler B is too small to split."));
        }
    }

    [Test, Description("Reasons from several blocked Forces are all reported, so fixing one does not reveal the next as a surprise.")]
    public void ShouldCollectTheReasonsFromEveryBlockedForceWhenSeveralAreBlocked()
    {
        ForceActionAvailability? result = ForceActionAggregator.Aggregate(_STRATEGY,
        [
            Verdict(1, "Settler A", ForceActionAvailability.Blocked(["Settler A is too small to split."])),
            Verdict(2, "Settler B", ForceActionAvailability.Blocked(["Settler B has already moved this turn."])),
            Verdict(3, "Warband", availability: null)
        ]);

        Assert.That(result!.DisabledReasons, Is.EqualTo(
        [
            "Settler A is too small to split.",
            "Settler B has already moved this turn.",
            "'Split Force' does not apply to Warband."
        ]), "Reasons should arrive in the order the Forces were judged, which the builder fixes by Force id.");
    }

    [Test, Description("A reason about the organisation rather than the Force is reported once, not once per Force sharing the tag.")]
    public void ShouldDeduplicateIdenticalReasonsWhenSeveralForcesReportTheSameFault()
    {
        const string CONFIGURATION_ERROR = "Organisation 'Settler' is Unled but has no authored base size, so its forces cannot be split.";

        ForceActionAvailability? result = ForceActionAggregator.Aggregate(_STRATEGY,
        [
            Verdict(1, "Settler A", ForceActionAvailability.Blocked([CONFIGURATION_ERROR])),
            Verdict(2, "Settler B", ForceActionAvailability.Blocked([CONFIGURATION_ERROR]))
        ]);

        Assert.That(result!.DisabledReasons, Is.EqualTo([CONFIGURATION_ERROR]),
            "A fact about the tag is one fact however many Forces carry that tag.");
    }

    [Test, Description("A selection of one behaves exactly as the single-Force toolbar always did, so nothing changed for the common case.")]
    public void ShouldPassASingleVerdictThroughUnchangedWhenOnlyOneForceIsSelected()
    {
        ForceActionAvailability? available = ForceActionAggregator.Aggregate(_STRATEGY,
            [Verdict(1, "Settler", ForceActionAvailability.Available())]);

        ForceActionAvailability? blocked = ForceActionAggregator.Aggregate(_STRATEGY,
            [Verdict(1, "Settler", ForceActionAvailability.Blocked(["Settler is too small to split."]))]);

        ForceActionAvailability? hidden = ForceActionAggregator.Aggregate(_STRATEGY,
            [Verdict(1, "Warband", availability: null)]);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(available!.IsEnabled, Is.True);
            Assert.That(blocked!.DisabledReasons.Single(), Is.EqualTo("Settler is too small to split."));
            Assert.That(hidden, Is.Null);
        }
    }

    [Test, Description("An empty selection yields no action, since there is nothing for a button to act on.")]
    public void ShouldHideTheActionWhenNothingIsSelected()
    {
        Assert.That(ForceActionAggregator.Aggregate(_STRATEGY, []), Is.Null);
    }

    /// <summary>A strategy that exists only to carry a label — the aggregator never evaluates it.</summary>
    private sealed class LabelOnlyStrategy : IForceActionStrategy
    {
        public ForceActionType ActionType => ForceActionType.SplitForce;

        public string Label => _LABEL;

        public ForceActionAvailability? Evaluate(ForceActionContext context)
        {
            throw new NotSupportedException("The aggregator folds verdicts it is handed; it never evaluates a strategy itself.");
        }
    }
}
