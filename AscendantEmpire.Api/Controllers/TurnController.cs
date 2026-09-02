using AscendantEmpire.Api.Utilities;
using AscendantEmpire.Application.Common;
using AscendantEmpire.Application.Features.Turns.Handlers;
using AscendantEmpire.Application.Features.Turns.Services;
using AscendantEmpire.BackendContracts.Turns.GetCurrentCampaignTurn;
using AscendantEmpire.Domain.Enums.Turns;
using AscendantEmpire.FrontendContracts;
using AscendantEmpire.FrontendContracts.Turns.EndCampaignTurnOrder;

using MediatR;

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

using static AscendantEmpire.Application.Common.ServiceResultBase;

namespace AscendantEmpire.Api.Controllers;

[ApiController]
[Route("[controller]")]
// Every action here needs a game token: the game and player are read from the token's claims,
// not from the request. A user token (no game entered yet) is rejected by TryGetGameContext.
[Authorize]
public class TurnController : ControllerBase
{
    /// <summary>
    /// Get a Player's position in the Turn Roster for the current Game
    /// </summary>
    /// <response code="200">Turn information for Campaign and Battle Maps</response>
    /// <response code="400">Returns an error response with an array of errors in the event of a failed request due to client error, such as invalid input</response>
    /// <response code="403">The caller has not entered a game</response>
    /// <response code="404">Returns an error response when a Game, Player or TurnQueue not found</response>
    /// <response code="500">Returns an error response with an array of errors in the event of an exception or other server error</response>
    [HttpGet("campaign")]
    [ProducesResponseType(typeof(CurrentCampaignTurn), 200)]
    [ProducesResponseType(typeof(IApiErrorResult), 400)]
    [ProducesResponseType(typeof(IApiErrorResult), 403)]
    [ProducesResponseType(typeof(IApiErrorResult), 404)]
    [ProducesResponseType(typeof(IApiErrorResult), 500)]
    public async Task<IActionResult> GetPlayerTurnAsync(
        [FromServices] ICampaignTurnService campaignTurnService,
        CancellationToken cancelToken)
    {
        // The game comes from the token, so the caller can't read another game's turn state.
        if (!this.TryGetGameContext(out int gameId, out int _, out IActionResult? contextError))
        {
            return contextError!;
        }

        ServiceResult<CurrentCampaignTurn> response = await campaignTurnService.GetCurrentCampaignTurnAsync(gameId, cancelToken);

        return response.ToActionResult();
    }

    /// <summary>
    /// End a Player's Campaign Turn
    /// </summary>
    /// <remarks>
    /// <para>Sent twice in the ordinary case. The first call asks in <see cref="EndTurnMode.CheckForActions"/> mode: anything the Player might still
    /// want to do comes back in <see cref="IEndCampaignTurnOutcome.PossibleActions"/> with <c>TurnEnded</c> false, and the Turn keeps running. The
    /// Player either deals with those or accepts them, and the client asks again with <paramref name="force"/> set to end the Turn regardless.</para>
    /// <para>When the first call finds nothing outstanding it ends the Turn there and then, so a Player with nothing left to do is not asked to
    /// confirm something the game already knows is finished.</para>
    /// </remarks>
    /// <param name="mediator">Mediator used to dispatch the end-turn request.</param>
    /// <param name="cancelToken">Cancellation token.</param>
    /// <param name="force">End the Turn regardless of anything the Player has left outstanding. Defaults to false, which checks first.</param>
    /// <response code="200">The outcome of the request: either the Turn ended, or the actions that are still outstanding</response>
    /// <response code="400">Returns an error response with an array of errors in the event of a failed request due to client error, such as invalid input</response>
    /// <response code="403">The caller has not entered a game</response>
    /// <response code="404">Returns an error response when a Game, Player or TurnQueue not found</response>
    /// <response code="500">Returns an error response with an array of errors in the event of an exception or other server error</response>
    [HttpPost("end-campaign")]
    [ProducesResponseType(typeof(IEndCampaignTurnOutcome), 200)]
    [ProducesResponseType(typeof(IApiErrorResult), 400)]
    [ProducesResponseType(typeof(IApiErrorResult), 403)]
    [ProducesResponseType(typeof(IApiErrorResult), 404)]
    [ProducesResponseType(typeof(IApiErrorResult), 500)]
    public async Task<IActionResult> EndPlayerTurnAsync(
        [FromServices] IMediator mediator,
        CancellationToken cancelToken,
        [FromQuery] bool force = false)
    {
        // Game and player come from the token, so the caller can't end someone else's turn. All the request carries is whether the Player has already
        // been shown what they have outstanding and chosen to end anyway — the identity is not theirs to choose.
        if (!this.TryGetGameContext(out int gameId, out int playerId, out IActionResult? contextError))
        {
            return contextError!;
        }

        EndTurnMode endTurnMode = force ? EndTurnMode.ForceEnd : EndTurnMode.CheckForActions;

        ServiceResult<EndCampaignTurnOutcome> response = await mediator.Send(new EndCampaignTurnRequest(gameId, playerId, endTurnMode), cancelToken);

        // The generic overload, so the outcome travels in the body — the non-generic one answers with a bare status code and the Player would never
        // see what they had outstanding.
        return response.ToActionResult();
    }
}
