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
        int unitInstanceId,
        CancellationToken cancelToken)
    {
        ForceActionContext? context = await contextBuilder.BuildAsync(gameId, playerId, unitInstanceId, cancelToken);

        if (context is null)
        {
            string error = $"Unit Instance Id {unitInstanceId} was not found for Player Id {playerId} in Game Id {gameId}.";
            logger.LogWarning("Unit Instance Id {UnitInstanceId} was not found for Player Id {PlayerId} in Game Id {GameId}.",
                unitInstanceId, playerId, gameId);

            return ServiceResultBase.ServiceResult<IReadOnlyCollection<ForceContextAction>>.NotFound(error);
        }

        List<ForceContextAction> actions = [];

        // Ordered by the action type's own value, so the toolbar's sequence is fixed by the enum rather than by registration order.
        foreach (IForceActionStrategy strategy in strategies.OrderBy(s => (int)s.ActionType))
        {
            cancelToken.ThrowIfCancellationRequested();

            ForceActionAvailability? availability;

            try
            {
                availability = strategy.Evaluate(context);
            }
            catch (Exception ex)
            {
                // One broken strategy must not cost the player their whole toolbar; drop just that action and carry on. Mirrors how the
                // end-turn possible-action pipeline isolates its checks.
                logger.LogError(ex, "Force action strategy '{ActionType}' threw for Unit Instance Id {UnitInstanceId} in Game Id {GameId}.",
                    strategy.ActionType, unitInstanceId, gameId);

                continue;
            }

            // Null means the action is structurally irrelevant to this Force, so it is left out of the toolbar altogether.
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

        logger.LogDebug("Resolved {Count} context action(s) for Unit Instance Id {UnitInstanceId} in Game Id {GameId}.",
            actions.Count, unitInstanceId, gameId);

        return ServiceResultBase.ServiceResult<IReadOnlyCollection<ForceContextAction>>.Succeed(actions);
    }

    public async Task<ServiceResultBase.ServiceResult<ForceActionAvailability>> EvaluateActionAsync(
        int gameId,
        int playerId,
        int unitInstanceId,
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

        ForceActionContext? context = await contextBuilder.BuildAsync(gameId, playerId, unitInstanceId, cancelToken);

        if (context is null)
        {
            string error = $"Unit Instance Id {unitInstanceId} was not found for Player Id {playerId} in Game Id {gameId}.";
            logger.LogWarning("Unit Instance Id {UnitInstanceId} was not found for Player Id {PlayerId} in Game Id {GameId}.",
                unitInstanceId, playerId, gameId);

            return ServiceResultBase.ServiceResult<ForceActionAvailability>.NotFound(error);
        }

        // A structurally irrelevant action (null) is a hard no at execution time, not merely an absent button — so it is reported as blocked
        // rather than allowed through.
        ForceActionAvailability availability = strategy.Evaluate(context)
            ?? ForceActionAvailability.Blocked([$"'{strategy.Label}' does not apply to this force."]);

        return ServiceResultBase.ServiceResult<ForceActionAvailability>.Succeed(availability);
    }
}
