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
    /// <response code="200">End Turn</response>
    /// <response code="400">Returns an error response with an array of errors in the event of a failed request due to client error, such as invalid input</response>
    /// <response code="403">The caller has not entered a game</response>
    /// <response code="404">Returns an error response when a Game, Player or TurnQueue not found</response>
    /// <response code="500">Returns an error response with an array of errors in the event of an exception or other server error</response>
    [HttpPost("end-campaign")]
    [ProducesResponseType(typeof(IEndCampaignTurnOutcome), 204)]
    [ProducesResponseType(typeof(IApiErrorResult), 400)]
    [ProducesResponseType(typeof(IApiErrorResult), 403)]
    [ProducesResponseType(typeof(IApiErrorResult), 404)]
    [ProducesResponseType(typeof(IApiErrorResult), 500)]
    public async Task<IActionResult> EndPlayerTurnAsync(
        [FromServices] IMediator mediator,
        CancellationToken cancelToken)
    {
        // Game and player come from the token, so the caller can't end someone else's turn. The action takes no body at all: there is nothing left for one
        // to carry now that the identity is no longer the caller's to choose.
        if (!this.TryGetGameContext(out int gameId, out int playerId, out IActionResult? contextError))
        {
            return contextError!;
        }

        ServiceResultBase response = await mediator.Send(new EndCampaignTurnRequest(gameId, playerId, EndTurnMode.CheckForActions), cancelToken);

        return response.ToActionResult();
    }
}
