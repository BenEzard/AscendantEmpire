using AscendantEmpire.Application.Common;
using AscendantEmpire.BackendContracts.ForceOrders.GetForceContextActions;
using AscendantEmpire.Domain.Enums.Forces;

namespace AscendantEmpire.Application.Features.ForceOrders.ContextActions;

/// <summary>
/// Aggregates the context actions available for a selected troop, and re-judges a single action when one is about to be executed.
/// </summary>
internal interface IForceContextActionService
{
    /// <summary>
    /// Every action worth showing for the selected troop, in the toolbar's fixed display order.
    /// </summary>
    Task<ServiceResultBase.ServiceResult<IReadOnlyCollection<ForceContextAction>>> GetAvailableActionsAsync(
        int gameId,
        int playerId,
        int unitInstanceId,
        CancellationToken cancelToken);

    /// <summary>
    /// Re-judges one action immediately before it is executed, against freshly loaded state.
    /// </summary>
    /// <remarks>
    /// Handlers which act on a troop must call this rather than trusting the client, and rather than re-implementing the rules: it runs the very
    /// same strategy the toolbar rendered from, so the two cannot disagree.
    /// </remarks>
    Task<ServiceResultBase.ServiceResult<ForceActionAvailability>> EvaluateActionAsync(
        int gameId,
        int playerId,
        int unitInstanceId,
        ForceActionType actionType,
        CancellationToken cancelToken);
}
