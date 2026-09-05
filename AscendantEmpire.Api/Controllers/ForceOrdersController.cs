using AscendantEmpire.Api.Utilities;
using AscendantEmpire.Application.Common;
using AscendantEmpire.Application.Features.ForceOrders.Classes;
using AscendantEmpire.Application.Features.ForceOrders.Handlers;
using AscendantEmpire.Application.Features.Maps.CampaignMaps.Classes;
using AscendantEmpire.BackendContracts.ForceOrders.GetForceContextActions;
using AscendantEmpire.BackendContracts.ForceOrders.MoveForces;
using AscendantEmpire.BackendContracts.ForceOrders.StepForce;
using AscendantEmpire.FrontendContracts;
using AscendantEmpire.FrontendContracts.ForceOrders.GetForceContextActions;
using AscendantEmpire.FrontendContracts.ForceOrders.StepForce;

using MediatR;

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AscendantEmpire.Api.Controllers;

[ApiController]
[Route("[controller]")]
// Every action here needs a game token: the game and player are read from the token's claims,
// not from the request. A user token (no game entered yet) is rejected by TryGetGameContext.
[Authorize]
public class ForceOrdersController : ControllerBase
{
    /// <summary>
    /// Order the Forces to move to a specific location on the map.
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
    public async Task<IActionResult> MoveForcesAsync(
        [FromServices] IMediator mediator,
        [FromBody] MoveForcesDto request)
    {
        // Game and player come from the token, so the caller can't order someone else's Forces around. The body carries only which Force moves and where.
        if (!this.TryGetGameContext(out int gameId, out int playerId, out IActionResult? contextError))
        {
            return contextError!;
        }

        ServiceResultBase response = await mediator.Send(new MoveForceRequest(
            gameId,
            playerId,
            request.UnitInstanceId,
            request.XCoord,
            request.YCoord));

        return response.ToActionResult();
    }

    /// <summary>
    /// Step a Force one tile in one of the eight compass directions, and answer with what changed.
    /// </summary>
    /// <remarks>
    /// <para>
    /// A single immediate move, not a standing order: it is validated, applied and paid for within the request, so no <c>ForceOrder</c> is raised.
    /// The multi-turn pathfound order is <c>move</c> above.
    /// </para>
    /// <para>
    /// The response carries the Force's new position and movement points together with the player's refreshed view of the map, so the client can
    /// patch the tiles it already holds — the one the Force left, the one it arrived on, and any newly revealed by the move — rather than reloading.
    /// </para>
    /// <para>A refusal is a 400 whose message is written to be shown to the player as it stands: which tile, and why it cannot be entered.</para>
    /// </remarks>
    /// <response code="200">The Force moved. Returns its new position, its remaining movement, and the refreshed map view</response>
    /// <response code="400">Validation error, or the step was refused — impassable terrain, the wrong movement domain, or not enough movement</response>
    /// <response code="403">The caller has not entered a game, does not own the Force, or it is not their turn</response>
    /// <response code="404">The Force is not in this game, or the destination is not a tile on the map</response>
    /// <response code="500">Returns an error response with an array of errors in the event of an exception or other server error</response>
    [HttpPost("step")]
    [ProducesResponseType(typeof(IStepForceResult), 200)]
    [ProducesResponseType(typeof(IApiErrorResult), 400)]
    [ProducesResponseType(typeof(IApiErrorResult), 403)]
    [ProducesResponseType(typeof(IApiErrorResult), 404)]
    [ProducesResponseType(typeof(IApiErrorResult), 500)]
    public async Task<IActionResult> StepForceAsync(
        [FromServices] IMediator mediator,
        [FromBody] StepForceDto request,
        CancellationToken cancelToken)
    {
        // Game and player come from the token, so the caller can't step someone else's Forces. The body carries only which Force moves and which way.
        if (!this.TryGetGameContext(out int gameId, out int playerId, out IActionResult? contextError))
        {
            return contextError!;
        }

        ServiceResultBase.ServiceResult<StepForceResult> response = await mediator.Send(
            new StepForceRequest(gameId, playerId, request.UnitInstanceId, request.Direction),
            cancelToken);

        return response.ToActionResult(contract => new DTStepForceResult
        {
            UnitInstanceId = contract.UnitInstanceId,
            XCoord = contract.XCoord,
            YCoord = contract.YCoord,
            CampaignMovementRemaining = contract.CampaignMovementRemaining,
            CampaignMovementPoints = contract.CampaignMovementPoints,
            // Shaped exactly as the fog-of-war read's response, so the client merges both with the same code.
            Observers = contract.Observers.ToPresentation()
        });
    }

    /// <summary>
    /// Get the context actions available to the selected Forces right now — what the toolbar should offer, and why anything offered is unavailable.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The player may have several Forces selected, and an action offered over a selection is a single order covering all of them. So every action
    /// is judged against every selected Force: it comes back enabled only when all of them can take it, disabled with the collected reasons when
    /// at least one of them could take it but cannot right now, and omitted entirely when none of them could ever take it.
    /// </para>
    /// <para>
    /// The same rules are re-checked when an action is actually executed, so the response is a rendering hint and never an authorisation.
    /// </para>
    /// </remarks>
    /// <param name="mediator">The mediator that dispatches the request.</param>
    /// <param name="id">
    /// One or more Force ids, repeated — <c>?id=1&amp;id=2</c>. Named simply <c>id</c> because a Force is a category rather than a type: every
    /// fielded Force is a Unit today so these are <c>UnitInstance</c> ids, but a Command is equally a Force and will arrive through this same
    /// parameter without a rename. Read it throughout as a Force instance id; see <c>docs/Terminology.md</c>.
    /// </param>
    /// <param name="cancelToken">Cancels the request.</param>
    /// <response code="200">The available actions, in their fixed display order</response>
    /// <response code="400">Validation error</response>
    /// <response code="403">The caller has not entered a game</response>
    /// <response code="404">One or more of the selected Forces is not in this game, or does not belong to this player</response>
    /// <response code="500">Returns an error response with an array of errors in the event of an exception or other server error</response>
    [HttpGet("actions")]
    [ProducesResponseType(typeof(IForceContextActionCollection), 200)]
    [ProducesResponseType(typeof(IApiErrorResult), 400)]
    [ProducesResponseType(typeof(IApiErrorResult), 403)]
    [ProducesResponseType(typeof(IApiErrorResult), 404)]
    [ProducesResponseType(typeof(IApiErrorResult), 500)]
    public async Task<IActionResult> GetForceContextActionsAsync(
        [FromServices] IMediator mediator,
        [FromQuery(Name = "id")] int[] id,
        CancellationToken cancelToken)
    {
        // Game and player come from the token, so a caller can't enumerate the actions available to someone else's Forces.
        if (!this.TryGetGameContext(out int gameId, out int playerId, out IActionResult? contextError))
        {
            return contextError!;
        }

        ServiceResultBase.ServiceResult<IReadOnlyCollection<ForceContextAction>> response = await mediator.Send(
            new GetForceContextActionsRequest(gameId, playerId, id ?? []),
            cancelToken);

        return response.ToActionResult(contract =>
        {
            return new DTForceContextActionCollection
            {
                Actions = [.. contract.Select(IForceContextAction (action) => new DTForceContextAction
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
