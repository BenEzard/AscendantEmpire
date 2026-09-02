using AscendantEmpire.Application.Common;
using AscendantEmpire.BackendContracts.ForceOrders.GetForceContextActions;
using AscendantEmpire.Domain.Enums.Forces;

namespace AscendantEmpire.Application.Features.ForceOrders.ContextActions;

/// <summary>
/// Aggregates the context actions available for a selected Force or Forces, and re-judges a single action when one is about to be executed.
/// </summary>
/// <remarks>
/// Both methods take a <b>selection</b>. The player may have several Forces selected at once, and an action offered over a selection is one order
/// covering all of them, so each is judged against every selected Force and the verdicts folded together by <see cref="ForceActionAggregator"/>.
/// A single Force is simply a selection of one.
/// </remarks>
internal interface IForceContextActionService
{
    /// <summary>
    /// Every action worth showing for the selected Forces, in the toolbar's fixed display order.
    /// </summary>
    /// <param name="forceIds">
    /// The selected Forces. Read these as Force ids: they are <c>UnitInstance</c> ids while Units are the only Force ever fielded, and will be
    /// Command ids too once Commands are — see <c>docs/Terminology.md</c>.
    /// </param>
    Task<ServiceResultBase.ServiceResult<IReadOnlyCollection<ForceContextAction>>> GetAvailableActionsAsync(
        int gameId,
        int playerId,
        IReadOnlyCollection<int> forceIds,
        CancellationToken cancelToken);

    /// <summary>
    /// Re-judges one action immediately before it is executed, against freshly loaded state.
    /// </summary>
    /// <remarks>
    /// Handlers which act on a selection must call this rather than trusting the client, and rather than re-implementing the rules: it runs the very
    /// same strategy the toolbar rendered from, over the very same aggregation, so the two cannot disagree.
    /// </remarks>
    Task<ServiceResultBase.ServiceResult<ForceActionAvailability>> EvaluateActionAsync(
        int gameId,
        int playerId,
        IReadOnlyCollection<int> forceIds,
        ForceActionType actionType,
        CancellationToken cancelToken);
}
