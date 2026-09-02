using AscendantEmpire.Application.Common;
using AscendantEmpire.BackendContracts.ForceOrders.GetForceContextActions;
using AscendantEmpire.Domain.Enums.Forces;

using Microsoft.Extensions.Logging;

namespace AscendantEmpire.Application.Features.ForceOrders.ContextActions;

/// <inheritdoc cref="IForceContextActionService"/>
internal sealed class ForceContextActionService(
    IEnumerable<IForceActionStrategy> strategies,
    IForceActionContextBuilder contextBuilder,
    ILogger<ForceContextActionService> logger) : IForceContextActionService
{
    public async Task<ServiceResultBase.ServiceResult<IReadOnlyCollection<ForceContextAction>>> GetAvailableActionsAsync(
        int gameId,
        int playerId,
        IReadOnlyCollection<int> forceIds,
        CancellationToken cancelToken)
    {
        IReadOnlyCollection<ForceActionContext>? contexts = await contextBuilder.BuildAsync(gameId, playerId, forceIds, cancelToken);

        if (contexts is null)
        {
            string error = $"{DescribeSelection(forceIds)} was not found for Player Id {playerId} in Game Id {gameId}.";
            logger.LogWarning("{Selection} was not found for Player Id {PlayerId} in Game Id {GameId}.",
                DescribeSelection(forceIds), playerId, gameId);

            return ServiceResultBase.ServiceResult<IReadOnlyCollection<ForceContextAction>>.NotFound(error);
        }

        List<ForceContextAction> actions = [];

        // Ordered by the action type's own value, so the toolbar's sequence is fixed by the enum rather than by registration order.
        foreach (IForceActionStrategy strategy in strategies.OrderBy(s => (int)s.ActionType))
        {
            cancelToken.ThrowIfCancellationRequested();

            List<(ForceActionContext Context, ForceActionAvailability? Verdict)> verdicts = [];
            bool strategyFaulted = false;

            foreach (ForceActionContext context in contexts)
            {
                try
                {
                    verdicts.Add((context, strategy.Evaluate(context)));
                }
                catch (Exception ex)
                {
                    // One broken strategy must not cost the player their whole toolbar; drop just that action and carry on. Mirrors how the
                    // end-turn possible-action pipeline isolates its checks.
                    //
                    // A throw against any one Force drops the action for the entire selection rather than for that Force alone: a partial
                    // aggregate would silently judge fewer Forces than are selected, which is worse than an absent button.
                    logger.LogError(ex, "Force action strategy '{ActionType}' threw for Unit Instance Id {UnitInstanceId} in Game Id {GameId}.",
                        strategy.ActionType, context.SelectedForce.UnitInstanceId, gameId);

                    strategyFaulted = true;

                    break;
                }
            }

            if (strategyFaulted)
            {
                continue;
            }

            ForceActionAvailability? availability = ForceActionAggregator.Aggregate(strategy, verdicts);

            // Null means the action is structurally irrelevant to every selected Force, so it is left out of the toolbar altogether.
            if (availability is null)
            {
                continue;
            }

            actions.Add(new ForceContextAction
            {
                ActionType = strategy.ActionType,
                Label = strategy.Label,
                DisplayOrder = (int)strategy.ActionType,
                IsEnabled = availability.IsEnabled,
                DisabledReasons = availability.DisabledReasons
            });
        }

        logger.LogDebug("Resolved {Count} context action(s) for {Selection} in Game Id {GameId}.",
            actions.Count, DescribeSelection(forceIds), gameId);

        return ServiceResultBase.ServiceResult<IReadOnlyCollection<ForceContextAction>>.Succeed(actions);
    }

    public async Task<ServiceResultBase.ServiceResult<ForceActionAvailability>> EvaluateActionAsync(
        int gameId,
        int playerId,
        IReadOnlyCollection<int> forceIds,
        ForceActionType actionType,
        CancellationToken cancelToken)
    {
        IForceActionStrategy? strategy = strategies.SingleOrDefault(s => s.ActionType == actionType);

        if (strategy is null)
        {
            string error = $"No force action strategy is registered for '{actionType}'.";
            logger.LogWarning("No force action strategy is registered for '{ActionType}'.", actionType);

            return ServiceResultBase.ServiceResult<ForceActionAvailability>.NotFound(error);
        }

        IReadOnlyCollection<ForceActionContext>? contexts = await contextBuilder.BuildAsync(gameId, playerId, forceIds, cancelToken);

        if (contexts is null)
        {
            string error = $"{DescribeSelection(forceIds)} was not found for Player Id {playerId} in Game Id {gameId}.";
            logger.LogWarning("{Selection} was not found for Player Id {PlayerId} in Game Id {GameId}.",
                DescribeSelection(forceIds), playerId, gameId);

            return ServiceResultBase.ServiceResult<ForceActionAvailability>.NotFound(error);
        }

        List<(ForceActionContext Context, ForceActionAvailability? Verdict)> verdicts =
            [.. contexts.Select(context => (context, strategy.Evaluate(context)))];

        // A structurally irrelevant action is a hard no at execution time, not merely an absent button — so an aggregate of nothing but nulls is
        // reported as blocked rather than allowed through.
        ForceActionAvailability availability = ForceActionAggregator.Aggregate(strategy, verdicts)
            ?? ForceActionAvailability.Blocked([$"'{strategy.Label}' does not apply to this force."]);

        return ServiceResultBase.ServiceResult<ForceActionAvailability>.Succeed(availability);
    }

    /// <summary>Names the selection for a log line or an error, reading naturally whether it holds one Force or several.</summary>
    private static string DescribeSelection(IReadOnlyCollection<int> forceIds)
    {
        if (forceIds is null || forceIds.Count == 0)
        {
            return "An empty Force selection";
        }

        if (forceIds.Count == 1)
        {
            return $"Unit Instance Id {forceIds.First()}";
        }

        return $"Unit Instance Ids {string.Join(", ", forceIds.Order())}";
    }
}
