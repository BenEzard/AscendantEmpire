using AscendantEmpire.Application.Common;
using AscendantEmpire.BackendContracts.TroopOrders.GetTroopContextActions;
using AscendantEmpire.Domain.Enums.Troops;

using Microsoft.Extensions.Logging;

namespace AscendantEmpire.Application.Features.TroopOrders.ContextActions;

/// <inheritdoc cref="ITroopContextActionService"/>
internal sealed class TroopContextActionService(
    IEnumerable<ITroopActionStrategy> strategies,
    ITroopActionContextBuilder contextBuilder,
    ILogger<TroopContextActionService> logger) : ITroopContextActionService
{
    public async Task<ServiceResultBase.ServiceResult<IReadOnlyCollection<TroopContextAction>>> GetAvailableActionsAsync(
        int gameId,
        int playerId,
        int unitInstanceId,
        CancellationToken cancelToken)
    {
        TroopActionContext? context = await contextBuilder.BuildAsync(gameId, playerId, unitInstanceId, cancelToken);

        if (context is null)
        {
            string error = $"Troop Instance Id {unitInstanceId} was not found for Player Id {playerId} in Game Id {gameId}.";
            logger.LogWarning("Troop Instance Id {UnitInstanceId} was not found for Player Id {PlayerId} in Game Id {GameId}.",
                unitInstanceId, playerId, gameId);

            return ServiceResultBase.ServiceResult<IReadOnlyCollection<TroopContextAction>>.NotFound(error);
        }

        List<TroopContextAction> actions = [];

        // Ordered by the action type's own value, so the toolbar's sequence is fixed by the enum rather than by registration order.
        foreach (ITroopActionStrategy strategy in strategies.OrderBy(s => (int)s.ActionType))
        {
            cancelToken.ThrowIfCancellationRequested();

            TroopActionAvailability? availability;

            try
            {
                availability = strategy.Evaluate(context);
            }
            catch (Exception ex)
            {
                // One broken strategy must not cost the player their whole toolbar; drop just that action and carry on. Mirrors how the
                // end-turn possible-action pipeline isolates its checks.
                logger.LogError(ex, "Troop action strategy '{ActionType}' threw for Troop Instance Id {UnitInstanceId} in Game Id {GameId}.",
                    strategy.ActionType, unitInstanceId, gameId);

                continue;
            }

            // Null means the action is structurally irrelevant to this troop, so it is left out of the toolbar altogether.
            if (availability is null)
            {
                continue;
            }

            actions.Add(new TroopContextAction
            {
                ActionType = strategy.ActionType,
                Label = strategy.Label,
                DisplayOrder = (int)strategy.ActionType,
                IsEnabled = availability.IsEnabled,
                DisabledReasons = availability.DisabledReasons
            });
        }

        logger.LogDebug("Resolved {Count} context action(s) for Troop Instance Id {UnitInstanceId} in Game Id {GameId}.",
            actions.Count, unitInstanceId, gameId);

        return ServiceResultBase.ServiceResult<IReadOnlyCollection<TroopContextAction>>.Succeed(actions);
    }

    public async Task<ServiceResultBase.ServiceResult<TroopActionAvailability>> EvaluateActionAsync(
        int gameId,
        int playerId,
        int unitInstanceId,
        TroopActionType actionType,
        CancellationToken cancelToken)
    {
        ITroopActionStrategy? strategy = strategies.SingleOrDefault(s => s.ActionType == actionType);

        if (strategy is null)
        {
            string error = $"No troop action strategy is registered for '{actionType}'.";
            logger.LogWarning("No troop action strategy is registered for '{ActionType}'.", actionType);

            return ServiceResultBase.ServiceResult<TroopActionAvailability>.NotFound(error);
        }

        TroopActionContext? context = await contextBuilder.BuildAsync(gameId, playerId, unitInstanceId, cancelToken);

        if (context is null)
        {
            string error = $"Troop Instance Id {unitInstanceId} was not found for Player Id {playerId} in Game Id {gameId}.";
            logger.LogWarning("Troop Instance Id {UnitInstanceId} was not found for Player Id {PlayerId} in Game Id {GameId}.",
                unitInstanceId, playerId, gameId);

            return ServiceResultBase.ServiceResult<TroopActionAvailability>.NotFound(error);
        }

        // A structurally irrelevant action (null) is a hard no at execution time, not merely an absent button — so it is reported as blocked
        // rather than allowed through.
        TroopActionAvailability availability = strategy.Evaluate(context)
            ?? TroopActionAvailability.Blocked([$"'{strategy.Label}' does not apply to this troop."]);

        return ServiceResultBase.ServiceResult<TroopActionAvailability>.Succeed(availability);
    }
}
