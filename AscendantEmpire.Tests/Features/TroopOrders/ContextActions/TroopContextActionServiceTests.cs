using AscendantEmpire.Application.Common;
using AscendantEmpire.Application.Features.TroopOrders.ContextActions;
using AscendantEmpire.BackendContracts.TroopOrders.GetTroopContextActions;
using AscendantEmpire.Domain.Enums.Troops;

using Microsoft.Extensions.Logging.Abstractions;

using NSubstitute;

namespace AscendantEmpire.Tests.Features.TroopOrders.ContextActions;

/// <summary>
/// Pure unit tests for <see cref="TroopContextActionService"/> — the context builder is substituted, so no database is involved.
/// </summary>
/// <remarks>
/// These cover the pipeline's own responsibilities (ordering, omission, fault isolation, re-validation) rather than any individual rule.
/// </remarks>
[TestFixture]
internal class TroopContextActionServiceTests
{
    private const int _GAME_ID = 1;
    private const int _PLAYER_ID = 1;
    private const int _TROOP_INSTANCE_ID = 5;

    /// <summary>An action type no strategy owns, used to prove ordering is by value rather than by registration.</summary>
    private const TroopActionType _EARLIER_ACTION_TYPE = (TroopActionType)500;

    private ITroopActionContextBuilder _contextBuilder = null!;

    [SetUp]
    public void SetUp()
    {
        _contextBuilder = Substitute.For<ITroopActionContextBuilder>();

        _contextBuilder.BuildAsync(_GAME_ID, _PLAYER_ID, _TROOP_INSTANCE_ID, Arg.Any<CancellationToken>())
            .Returns(BuildContext());
    }

    private static TroopActionContext BuildContext()
    {
        return new TroopActionContext
        {
            SelectedTroop = new TroopActionTroop
            {
                UnitInstanceId = _TROOP_INSTANCE_ID,
                PlayerId = _PLAYER_ID,
                CampaignMapTileId = 112,
                DisplayName = "Settler",
                HealthyCount = 100,
                InjuredCount = 0,
                MovementPointsRemaining = 1_000,
                CampaignMovementPoints = 1_000,
                Organisation = null
            },
            OtherTroopsOnTile = []
        };
    }

    private TroopContextActionService BuildService(params ITroopActionStrategy[] strategies)
    {
        return new TroopContextActionService(strategies, _contextBuilder, NullLogger<TroopContextActionService>.Instance);
    }

    private async Task<IReadOnlyCollection<TroopContextAction>> GetActionsAsync(params ITroopActionStrategy[] strategies)
    {
        ServiceResultBase.ServiceResult<IReadOnlyCollection<TroopContextAction>> result =
            await BuildService(strategies).GetAvailableActionsAsync(_GAME_ID, _PLAYER_ID, _TROOP_INSTANCE_ID, CancellationToken.None);

        Assert.That(result.HasSucceeded, Is.True);

        return result.SuccessValue!;
    }

    [Test, Description("The toolbar's order comes from the action type's value, not the order strategies happen to be registered in. If registration "
        + "order leaked through, editing a DI line would silently rearrange the player's buttons.")]
    public async Task ShouldOrderActionsByActionTypeWhenStrategiesAreRegisteredOutOfOrderAsync()
    {
        var later = new StubStrategy(TroopActionType.SplitTroops, TroopActionAvailability.Available());
        var earlier = new StubStrategy(_EARLIER_ACTION_TYPE, TroopActionAvailability.Available());

        IReadOnlyCollection<TroopContextAction> actions = await GetActionsAsync(later, earlier);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(actions.Select(a => a.ActionType), Is.EqualTo(new[] { _EARLIER_ACTION_TYPE, TroopActionType.SplitTroops }));
            Assert.That(actions.Select(a => a.DisplayOrder), Is.EqualTo(new[] { 500, 1000 }), "Display order should mirror the enum value.");
        }
    }

    [Test, Description("A null verdict means the action is structurally irrelevant to this troop, so it must not reach the toolbar at all. Sending it "
        + "disabled instead would fill the toolbar with buttons the troop could never use.")]
    public async Task ShouldOmitTheActionWhenItsStrategyReturnsNullAsync()
    {
        var hidden = new StubStrategy(TroopActionType.SplitTroops, availability: null);
        var shown = new StubStrategy(_EARLIER_ACTION_TYPE, TroopActionAvailability.Available());

        IReadOnlyCollection<TroopContextAction> actions = await GetActionsAsync(hidden, shown);

        Assert.That(actions.Select(a => a.ActionType), Is.EqualTo(new[] { _EARLIER_ACTION_TYPE }));
    }

    [Test, Description("A bug in one strategy should cost the player that one button, not the whole toolbar. Letting the exception escape would take "
        + "down every unrelated action too, which is the same isolation the end-turn possible-action pipeline gives its checks.")]
    public async Task ShouldDropOnlyTheFailingActionWhenAStrategyThrowsAsync()
    {
        var broken = new ThrowingStrategy(_EARLIER_ACTION_TYPE);
        var healthy = new StubStrategy(TroopActionType.SplitTroops, TroopActionAvailability.Available());

        IReadOnlyCollection<TroopContextAction> actions = await GetActionsAsync(broken, healthy);

        Assert.That(actions.Select(a => a.ActionType), Is.EqualTo(new[] { TroopActionType.SplitTroops }),
            "The surviving strategy should still produce its action.");
    }

    [Test, Description("The reasons are the entire point of sending a disabled action rather than hiding it, so they must survive onto the contract "
        + "intact. A disabled button with no explanation tells the player nothing.")]
    public async Task ShouldCarryDisabledReasonsOntoTheContractWhenAnActionIsBlockedAsync()
    {
        var blocked = new StubStrategy(TroopActionType.SplitTroops, TroopActionAvailability.Blocked(["Too small.", "Already moved."]));

        TroopContextAction action = (await GetActionsAsync(blocked)).Single();

        using (Assert.EnterMultipleScope())
        {
            Assert.That(action.IsEnabled, Is.False);
            Assert.That(action.DisabledReasons, Is.EqualTo(new[] { "Too small.", "Already moved." }));
            Assert.That(action.Label, Is.EqualTo(StubStrategy.LABEL));
        }
    }

    [Test, Description("A troop that doesn't exist, or isn't the caller's, yields no context. That is a 404 rather than an empty toolbar, because "
        + "an empty list would read as 'this troop can do nothing' instead of 'there is no such troop'.")]
    public async Task ShouldReturnNotFoundWhenTheTroopCannotBeLoadedAsync()
    {
        _contextBuilder.BuildAsync(_GAME_ID, _PLAYER_ID, _TROOP_INSTANCE_ID, Arg.Any<CancellationToken>())
            .Returns((TroopActionContext?)null);

        ServiceResultBase.ServiceResult<IReadOnlyCollection<TroopContextAction>> result = await BuildService()
            .GetAvailableActionsAsync(_GAME_ID, _PLAYER_ID, _TROOP_INSTANCE_ID, CancellationToken.None);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(result.HasSucceeded, Is.False);
            Assert.That(result.StatusCode, Is.EqualTo(ServiceResultStatus.NotFound));
        }
    }

    [Test, Description("Execution-time re-validation runs the very same strategy the toolbar rendered from. That shared path is what stops the "
        + "displayed decision and the enforced one drifting apart as rules change.")]
    public async Task ShouldReturnTheStrategysVerdictWhenReValidatingAnActionAsync()
    {
        var blocked = new StubStrategy(TroopActionType.SplitTroops, TroopActionAvailability.Blocked(["Already moved."]));

        ServiceResultBase.ServiceResult<TroopActionAvailability> result = await BuildService(blocked)
            .EvaluateActionAsync(_GAME_ID, _PLAYER_ID, _TROOP_INSTANCE_ID, TroopActionType.SplitTroops, CancellationToken.None);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(result.HasSucceeded, Is.True);
            Assert.That(result.SuccessValue!.IsEnabled, Is.False);
            Assert.That(result.SuccessValue.DisabledReasons.Single(), Is.EqualTo("Already moved."));
        }
    }

    [Test, Description("An action the toolbar would have hidden must be refused outright when someone posts it anyway. Treating 'no button existed' as "
        + "'no rule objected' would let a crafted request perform an action the troop can never legitimately take.")]
    public async Task ShouldBlockExecutionWhenTheStrategyHidesTheActionAsync()
    {
        var hidden = new StubStrategy(TroopActionType.SplitTroops, availability: null);

        ServiceResultBase.ServiceResult<TroopActionAvailability> result = await BuildService(hidden)
            .EvaluateActionAsync(_GAME_ID, _PLAYER_ID, _TROOP_INSTANCE_ID, TroopActionType.SplitTroops, CancellationToken.None);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(result.HasSucceeded, Is.True);
            Assert.That(result.SuccessValue!.IsEnabled, Is.False, "A hidden action must not be executable.");
            Assert.That(result.SuccessValue.DisabledReasons.Single(), Does.Contain("does not apply"));
        }
    }

    [Test, Description("Asking to execute an action nothing implements is a programming error, not a refusal — reporting it as 'not found' rather than "
        + "'blocked' keeps it distinguishable from a troop that merely fails the rules.")]
    public async Task ShouldReturnNotFoundWhenNoStrategyIsRegisteredForTheActionAsync()
    {
        ServiceResultBase.ServiceResult<TroopActionAvailability> result = await BuildService()
            .EvaluateActionAsync(_GAME_ID, _PLAYER_ID, _TROOP_INSTANCE_ID, TroopActionType.SplitTroops, CancellationToken.None);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(result.HasSucceeded, Is.False);
            Assert.That(result.StatusCode, Is.EqualTo(ServiceResultStatus.NotFound));
        }
    }

    /// <summary>A strategy that always returns the verdict it was handed.</summary>
    private sealed class StubStrategy(TroopActionType actionType, TroopActionAvailability? availability) : ITroopActionStrategy
    {
        public const string LABEL = "Stub Action";

        public TroopActionType ActionType => actionType;

        public string Label => LABEL;

        public TroopActionAvailability? Evaluate(TroopActionContext context)
        {
            return availability;
        }
    }

    /// <summary>A strategy that always fails, standing in for one with a bug in its rules.</summary>
    private sealed class ThrowingStrategy(TroopActionType actionType) : ITroopActionStrategy
    {
        public TroopActionType ActionType => actionType;

        public string Label => "Throwing Action";

        public TroopActionAvailability? Evaluate(TroopActionContext context)
        {
            throw new InvalidOperationException("This strategy is deliberately broken.");
        }
    }
}
