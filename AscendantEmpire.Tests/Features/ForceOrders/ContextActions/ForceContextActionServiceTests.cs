using AscendantEmpire.Application.Common;
using AscendantEmpire.Application.Features.ForceOrders.ContextActions;
using AscendantEmpire.BackendContracts.ForceOrders.GetForceContextActions;
using AscendantEmpire.Domain.Enums.Forces;
using AscendantEmpire.Domain.Enums.Maps;

using Microsoft.Extensions.Logging.Abstractions;

using NSubstitute;

namespace AscendantEmpire.Tests.Features.ForceOrders.ContextActions;

/// <summary>
/// Pure unit tests for <see cref="ForceContextActionService"/> — the context builder is substituted, so no database is involved.
/// </summary>
/// <remarks>
/// These cover the pipeline's own responsibilities (ordering, omission, fault isolation, re-validation) rather than any individual rule.
/// How several Forces' verdicts fold into one is <see cref="ForceActionAggregator"/>'s job and is tested there; what is pinned here is that the
/// service evaluates every selected Force and hands the results over.
/// </remarks>
[TestFixture]
internal class ForceContextActionServiceTests
{
    private const int _GAME_ID = 1;
    private const int _PLAYER_ID = 1;
    private const int _FORCE_ID = 5;
    private const int _SECOND_FORCE_ID = 6;

    /// <summary>An action type no strategy owns, used to prove ordering is by value rather than by registration.</summary>
    private const ForceActionType _EARLIER_ACTION_TYPE = (ForceActionType)500;

    private IForceActionContextBuilder _contextBuilder = null!;

    [SetUp]
    public void SetUp()
    {
        _contextBuilder = Substitute.For<IForceActionContextBuilder>();

        SetUpSelection(_FORCE_ID);
    }

    /// <summary>Points the substituted builder at a selection of the given Forces, one context each.</summary>
    private void SetUpSelection(params int[] forceIds)
    {
        _contextBuilder.BuildAsync(_GAME_ID, _PLAYER_ID, Arg.Any<IReadOnlyCollection<int>>(), Arg.Any<CancellationToken>())
            .Returns<IReadOnlyCollection<ForceActionContext>?>([.. forceIds.Select(BuildContext)]);
    }

    private static ForceActionContext BuildContext(int forceId)
    {
        return new ForceActionContext
        {
            SelectedForce = new ForceActionForce
            {
                UnitInstanceId = forceId,
                PlayerId = _PLAYER_ID,
                CampaignMapTileId = 112,
                DisplayName = $"Settler #{forceId}",
                HealthyCount = 100,
                InjuredCount = 0,
                CampaignMovementRemaining = 1_000,
                CampaignMovementPoints = 1_000,
                Organisation = null,
                CapabilityTagCodes = [],
                TileIsWater = false,
                TileGeographyType = CampaignMapGeographyType.Flat
            },
            OtherForcesOnTile = []
        };
    }

    private ForceContextActionService BuildService(params IForceActionStrategy[] strategies)
    {
        return new ForceContextActionService(strategies, _contextBuilder, NullLogger<ForceContextActionService>.Instance);
    }

    private async Task<IReadOnlyCollection<ForceContextAction>> GetActionsAsync(params IForceActionStrategy[] strategies)
    {
        ServiceResultBase.ServiceResult<IReadOnlyCollection<ForceContextAction>> result =
            await BuildService(strategies).GetAvailableActionsAsync(_GAME_ID, _PLAYER_ID, [_FORCE_ID], CancellationToken.None);

        Assert.That(result.HasSucceeded, Is.True);

        return result.SuccessValue!;
    }

    [Test, Description("Toolbar order comes from the action type's value, so editing a DI line cannot rearrange the player's buttons.")]
    public async Task ShouldOrderActionsByActionTypeWhenStrategiesAreRegisteredOutOfOrderAsync()
    {
        var later = new StubStrategy(ForceActionType.SplitForce, ForceActionAvailability.Available());
        var earlier = new StubStrategy(_EARLIER_ACTION_TYPE, ForceActionAvailability.Available());

        IReadOnlyCollection<ForceContextAction> actions = await GetActionsAsync(later, earlier);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(actions.Select(a => a.ActionType), Is.EqualTo(new[] { _EARLIER_ACTION_TYPE, ForceActionType.SplitForce }));
            Assert.That(actions.Select(a => a.DisplayOrder), Is.EqualTo(new[] { 500, 1000 }), "Display order should mirror the enum value.");
        }
    }

    [Test, Description("A null verdict means structurally irrelevant, so the action is omitted rather than shown disabled.")]
    public async Task ShouldOmitTheActionWhenItsStrategyReturnsNullAsync()
    {
        var hidden = new StubStrategy(ForceActionType.SplitForce, availability: null);
        var shown = new StubStrategy(_EARLIER_ACTION_TYPE, ForceActionAvailability.Available());

        IReadOnlyCollection<ForceContextAction> actions = await GetActionsAsync(hidden, shown);

        Assert.That(actions.Select(a => a.ActionType), Is.EqualTo(new[] { _EARLIER_ACTION_TYPE }));
    }

    [Test, Description("A throwing strategy costs the player that one button, not the whole toolbar.")]
    public async Task ShouldDropOnlyTheFailingActionWhenAStrategyThrowsAsync()
    {
        var broken = new ThrowingStrategy(_EARLIER_ACTION_TYPE);
        var healthy = new StubStrategy(ForceActionType.SplitForce, ForceActionAvailability.Available());

        IReadOnlyCollection<ForceContextAction> actions = await GetActionsAsync(broken, healthy);

        Assert.That(actions.Select(a => a.ActionType), Is.EqualTo(new[] { ForceActionType.SplitForce }),
            "The surviving strategy should still produce its action.");
    }

    [Test, Description("Reasons survive onto the contract intact, since a disabled button without one tells the player nothing.")]
    public async Task ShouldCarryDisabledReasonsOntoTheContractWhenAnActionIsBlockedAsync()
    {
        var blocked = new StubStrategy(ForceActionType.SplitForce, ForceActionAvailability.Blocked(["Too small.", "Already moved."]));

        ForceContextAction action = (await GetActionsAsync(blocked)).Single();

        using (Assert.EnterMultipleScope())
        {
            Assert.That(action.IsEnabled, Is.False);
            Assert.That(action.DisabledReasons, Is.EqualTo(new[] { "Too small.", "Already moved." }));
            Assert.That(action.Label, Is.EqualTo(StubStrategy.LABEL));
        }
    }

    [Test, Description("Every selected Force is judged, so one Force's block disables the action for the whole selection.")]
    public async Task ShouldEvaluateEverySelectedForceWhenSeveralAreSelectedAsync()
    {
        SetUpSelection(_FORCE_ID, _SECOND_FORCE_ID);

        // Only the second Force is blocked. If the service evaluated the first Force alone, this would come back enabled.
        var strategy = new PerForceStrategy(ForceActionType.SplitForce, new Dictionary<int, ForceActionAvailability?>
        {
            [_FORCE_ID] = ForceActionAvailability.Available(),
            [_SECOND_FORCE_ID] = ForceActionAvailability.Blocked(["Settler #6 has already moved this turn."])
        });

        ServiceResultBase.ServiceResult<IReadOnlyCollection<ForceContextAction>> result = await BuildService(strategy)
            .GetAvailableActionsAsync(_GAME_ID, _PLAYER_ID, [_FORCE_ID, _SECOND_FORCE_ID], CancellationToken.None);

        ForceContextAction action = result.SuccessValue!.Single();

        using (Assert.EnterMultipleScope())
        {
            Assert.That(action.IsEnabled, Is.False);
            Assert.That(action.DisabledReasons.Single(), Is.EqualTo("Settler #6 has already moved this turn."));
        }
    }

    [Test, Description("A strategy that throws for one Force of a selection drops the action entirely rather than judging the rest without it.")]
    public async Task ShouldDropTheActionWhenAStrategyThrowsForOnlyOneSelectedForceAsync()
    {
        SetUpSelection(_FORCE_ID, _SECOND_FORCE_ID);

        var partiallyBroken = new PerForceStrategy(ForceActionType.SplitForce, new Dictionary<int, ForceActionAvailability?>
        {
            [_FORCE_ID] = ForceActionAvailability.Available()
        });

        var healthy = new StubStrategy(_EARLIER_ACTION_TYPE, ForceActionAvailability.Available());

        ServiceResultBase.ServiceResult<IReadOnlyCollection<ForceContextAction>> result = await BuildService(partiallyBroken, healthy)
            .GetAvailableActionsAsync(_GAME_ID, _PLAYER_ID, [_FORCE_ID, _SECOND_FORCE_ID], CancellationToken.None);

        Assert.That(result.SuccessValue!.Select(a => a.ActionType), Is.EqualTo(new[] { _EARLIER_ACTION_TYPE }),
            "A partial verdict would judge fewer Forces than are selected, so the whole action goes.");
    }

    [Test, Description("A missing or foreign force is a 404, since an empty toolbar would read as 'this force can do nothing'.")]
    public async Task ShouldReturnNotFoundWhenTheForceCannotBeLoadedAsync()
    {
        _contextBuilder.BuildAsync(_GAME_ID, _PLAYER_ID, Arg.Any<IReadOnlyCollection<int>>(), Arg.Any<CancellationToken>())
            .Returns((IReadOnlyCollection<ForceActionContext>?)null);

        ServiceResultBase.ServiceResult<IReadOnlyCollection<ForceContextAction>> result = await BuildService()
            .GetAvailableActionsAsync(_GAME_ID, _PLAYER_ID, [_FORCE_ID], CancellationToken.None);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(result.HasSucceeded, Is.False);
            Assert.That(result.StatusCode, Is.EqualTo(ServiceResultStatus.NotFound));
        }
    }

    [Test, Description("One unloadable Force in a selection fails the whole request, rather than silently judging the Forces that did load.")]
    public async Task ShouldReturnNotFoundWhenOnlyPartOfTheSelectionCanBeLoadedAsync()
    {
        // The builder is all-or-nothing: it answers null for the selection rather than returning the Forces it could find.
        _contextBuilder.BuildAsync(_GAME_ID, _PLAYER_ID, Arg.Any<IReadOnlyCollection<int>>(), Arg.Any<CancellationToken>())
            .Returns((IReadOnlyCollection<ForceActionContext>?)null);

        var available = new StubStrategy(ForceActionType.SplitForce, ForceActionAvailability.Available());

        ServiceResultBase.ServiceResult<IReadOnlyCollection<ForceContextAction>> result = await BuildService(available)
            .GetAvailableActionsAsync(_GAME_ID, _PLAYER_ID, [_FORCE_ID, _SECOND_FORCE_ID], CancellationToken.None);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(result.HasSucceeded, Is.False);
            Assert.That(result.StatusCode, Is.EqualTo(ServiceResultStatus.NotFound));
        }
    }

    [Test, Description("Execution re-validates through the same strategy the toolbar rendered from, so the two cannot drift apart.")]
    public async Task ShouldReturnTheStrategysVerdictWhenReValidatingAnActionAsync()
    {
        var blocked = new StubStrategy(ForceActionType.SplitForce, ForceActionAvailability.Blocked(["Already moved."]));

        ServiceResultBase.ServiceResult<ForceActionAvailability> result = await BuildService(blocked)
            .EvaluateActionAsync(_GAME_ID, _PLAYER_ID, [_FORCE_ID], ForceActionType.SplitForce, CancellationToken.None);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(result.HasSucceeded, Is.True);
            Assert.That(result.SuccessValue!.IsEnabled, Is.False);
            Assert.That(result.SuccessValue.DisabledReasons.Single(), Is.EqualTo("Already moved."));
        }
    }

    [Test, Description("Execution re-validates the whole selection, so an action cannot be performed on a Force that could not take it.")]
    public async Task ShouldBlockExecutionWhenOnlyOneSelectedForceIsBlockedAsync()
    {
        SetUpSelection(_FORCE_ID, _SECOND_FORCE_ID);

        var strategy = new PerForceStrategy(ForceActionType.SplitForce, new Dictionary<int, ForceActionAvailability?>
        {
            [_FORCE_ID] = ForceActionAvailability.Available(),
            [_SECOND_FORCE_ID] = ForceActionAvailability.Blocked(["Settler #6 is too small to split."])
        });

        ServiceResultBase.ServiceResult<ForceActionAvailability> result = await BuildService(strategy)
            .EvaluateActionAsync(_GAME_ID, _PLAYER_ID, [_FORCE_ID, _SECOND_FORCE_ID], ForceActionType.SplitForce, CancellationToken.None);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(result.SuccessValue!.IsEnabled, Is.False);
            Assert.That(result.SuccessValue.DisabledReasons.Single(), Is.EqualTo("Settler #6 is too small to split."));
        }
    }

    [Test, Description("An action the toolbar would hide is refused when posted anyway, so a crafted request cannot perform it.")]
    public async Task ShouldBlockExecutionWhenTheStrategyHidesTheActionAsync()
    {
        var hidden = new StubStrategy(ForceActionType.SplitForce, availability: null);

        ServiceResultBase.ServiceResult<ForceActionAvailability> result = await BuildService(hidden)
            .EvaluateActionAsync(_GAME_ID, _PLAYER_ID, [_FORCE_ID], ForceActionType.SplitForce, CancellationToken.None);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(result.HasSucceeded, Is.True);
            Assert.That(result.SuccessValue!.IsEnabled, Is.False, "A hidden action must not be executable.");
            Assert.That(result.SuccessValue.DisabledReasons.Single(), Does.Contain("does not apply"));
        }
    }

    [Test, Description("An action nothing implements reports as not-found, keeping it distinguishable from one the rules blocked.")]
    public async Task ShouldReturnNotFoundWhenNoStrategyIsRegisteredForTheActionAsync()
    {
        ServiceResultBase.ServiceResult<ForceActionAvailability> result = await BuildService()
            .EvaluateActionAsync(_GAME_ID, _PLAYER_ID, [_FORCE_ID], ForceActionType.SplitForce, CancellationToken.None);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(result.HasSucceeded, Is.False);
            Assert.That(result.StatusCode, Is.EqualTo(ServiceResultStatus.NotFound));
        }
    }

    /// <summary>A strategy that always returns the verdict it was handed, whichever Force it is judging.</summary>
    private sealed class StubStrategy(ForceActionType actionType, ForceActionAvailability? availability) : IForceActionStrategy
    {
        public const string LABEL = "Stub Action";

        public ForceActionType ActionType => actionType;

        public string Label => LABEL;

        public ForceActionAvailability? Evaluate(ForceActionContext context)
        {
            return availability;
        }
    }

    /// <summary>
    /// A strategy with a different verdict per Force, for the cases where a selection must not be judged by its first member alone.
    /// </summary>
    /// <remarks>A Force missing from the map throws, which doubles as the "broken for one Force of several" case.</remarks>
    private sealed class PerForceStrategy(ForceActionType actionType, IReadOnlyDictionary<int, ForceActionAvailability?> verdictsByForceId)
        : IForceActionStrategy
    {
        public ForceActionType ActionType => actionType;

        public string Label => "Per Force Action";

        public ForceActionAvailability? Evaluate(ForceActionContext context)
        {
            if (!verdictsByForceId.TryGetValue(context.SelectedForce.UnitInstanceId, out ForceActionAvailability? availability))
            {
                throw new InvalidOperationException($"No verdict was set up for Unit Instance Id {context.SelectedForce.UnitInstanceId}.");
            }

            return availability;
        }
    }

    /// <summary>A strategy that always fails, standing in for one with a bug in its rules.</summary>
    private sealed class ThrowingStrategy(ForceActionType actionType) : IForceActionStrategy
    {
        public ForceActionType ActionType => actionType;

        public string Label => "Throwing Action";

        public ForceActionAvailability? Evaluate(ForceActionContext context)
        {
            throw new InvalidOperationException("This strategy is deliberately broken.");
        }
    }
}
