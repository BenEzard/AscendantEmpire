using AscendantEmpire.Api.Utilities;
using AscendantEmpire.Application.Common;
using AscendantEmpire.Application.Features.TroopOrders.Classes;
using AscendantEmpire.Application.Features.TroopOrders.Handlers;
using AscendantEmpire.BackendContracts.TroopOrders.GetTroopContextActions;
using AscendantEmpire.BackendContracts.TroopOrders.MoveTroops;
using AscendantEmpire.FrontendContracts;
using AscendantEmpire.FrontendContracts.TroopOrders.GetTroopContextActions;

using MediatR;

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AscendantEmpire.Api.Controllers;

[ApiController]
[Route("[controller]")]
// Every action here needs a game token: the game and player are read from the token's claims,
// not from the request. A user token (no game entered yet) is rejected by TryGetGameContext.
[Authorize]
public class TroopOrdersController : ControllerBase
{
    /// <summary>
    /// Order the troops to move to a specific location on the map.
    /// </summary>
    /// <response code="201">Game successfully created</response>
    /// <response code="400">Validation error</response>
    /// <response code="403">The caller has not entered a game</response>
    /// <response code="500">Returns an error response with an array of errors in the event of an exception or other server error</response>
    [HttpPost("move")]
    [ProducesResponseType(201)]
    [ProducesResponseType(typeof(IApiErrorResult), 400)]
    [ProducesResponseType(typeof(IApiErrorResult), 403)]
    [ProducesResponseType(typeof(IApiErrorResult), 500)]
    public async Task<IActionResult> MoveTroopsAsync(
        [FromServices] IMediator mediator,
        [FromBody] MoveTroopsDto request)
    {
        // Game and player come from the token, so the caller can't order someone else's troops around. The body carries only which troop moves and where.
        if (!this.TryGetGameContext(out int gameId, out int playerId, out IActionResult? contextError))
        {
            return contextError!;
        }

        ServiceResultBase response = await mediator.Send(new MoveTroopRequest(
            gameId,
            playerId,
            request.UnitInstanceId,
            request.XCoord,
            request.YCoord));

        return response.ToActionResult();
    }

    /// <summary>
    /// Get the context actions available to a selected troop right now — what the toolbar should offer, and why anything offered is unavailable.
    /// </summary>
    /// <remarks>
    /// Actions the troop could never take are omitted entirely; actions it could take but currently cannot come back disabled, with the reasons.
    /// The same rules are re-checked when an action is actually executed, so the response is a rendering hint and never an authorisation.
    /// </remarks>
    /// <response code="200">The available actions, in their fixed display order</response>
    /// <response code="400">Validation error</response>
    /// <response code="403">The caller has not entered a game</response>
    /// <response code="404">No such troop in this game for this player</response>
    /// <response code="500">Returns an error response with an array of errors in the event of an exception or other server error</response>
    [HttpGet("units/{unitInstanceId:int}/actions")]
    [ProducesResponseType(typeof(ITroopContextActionCollection), 200)]
    [ProducesResponseType(typeof(IApiErrorResult), 400)]
    [ProducesResponseType(typeof(IApiErrorResult), 403)]
    [ProducesResponseType(typeof(IApiErrorResult), 404)]
    [ProducesResponseType(typeof(IApiErrorResult), 500)]
    public async Task<IActionResult> GetTroopContextActionsAsync(
        [FromServices] IMediator mediator,
        int unitInstanceId,
        CancellationToken cancelToken)
    {
        // Game and player come from the token, so a caller can't enumerate the actions available to someone else's troops.
        if (!this.TryGetGameContext(out int gameId, out int playerId, out IActionResult? contextError))
        {
            return contextError!;
        }

        ServiceResultBase.ServiceResult<IReadOnlyCollection<TroopContextAction>> response = await mediator.Send(
            new GetTroopContextActionsRequest(gameId, playerId, unitInstanceId),
            cancelToken);

        return response.ToActionResult(contract =>
        {
            return new DTTroopContextActionCollection
            {
                Actions = [.. contract.Select(ITroopContextAction (action) => new DTTroopContextAction
                {
                    ActionType = action.ActionType,
                    Label = action.Label,
                    DisplayOrder = action.DisplayOrder,
                    IsEnabled = action.IsEnabled,
                    DisabledReasons = action.DisabledReasons
                })]
            };
        });
    }
}
