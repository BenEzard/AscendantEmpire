using AscendantEmpire.Api.Utilities;
using AscendantEmpire.Application.Common;
using AscendantEmpire.Application.Features.MatchMaking.Classes;
using AscendantEmpire.Application.Features.MatchMaking.Handlers;
using AscendantEmpire.BackendContracts.MatchMaking;
using AscendantEmpire.FrontendContracts;
using AscendantEmpire.FrontendContracts.MatchMaking;

using MediatR;

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

using static AscendantEmpire.Application.Common.ServiceResultBase;

namespace AscendantEmpire.Api.Controllers;

/// <summary>
/// Lets a logged-in user see joinable games and claim an unassigned player slot in one. Every
/// action requires a valid user token; the account is always read from that token.
/// </summary>
[ApiController]
[Route("[controller]")]
[Authorize]
public class MatchmakingController : ControllerBase
{
    /// <summary>
    /// List the games that can be joined, with each active slot flagged as claimed or free.
    /// </summary>
    /// <response code="200">The list of joinable games and their slots.</response>
    /// <response code="401">Missing or invalid token</response>
    /// <response code="500">Returns an error response with an array of errors in the event of an exception or other server error</response>
    [HttpGet("games")]
    [ProducesResponseType(typeof(IMatchmakingGameCollection), 200)]
    [ProducesResponseType(401)]
    [ProducesResponseType(typeof(IApiErrorResult), 500)]
    public async Task<IActionResult> GetGamesAsync(
        [FromServices] IMediator mediator,
        CancellationToken cancelToken)
    {
        // The user id comes from the proven token, never the request.
        int? userId = User.GetUserId();
        if (userId is null)
        {
            return Unauthorized();
        }

        ServiceResult<IReadOnlyCollection<MatchmakingGame>> response =
            await mediator.Send(new GetMatchmakingGamesRequest(userId.Value), cancelToken);

        return response.ToActionResult(games => new DTMatchmakingGameCollection
        {
            Games = response.SuccessValue?.Select(g => new DTMatchmakingGame
            {
                GameId = g.GameId,
                GameName = g.GameName,
                NumberOfPlayers = g.NumberOfPlayers,
                MapWidth = g.MapWidth,
                MapHeight = g.MapHeight,
                Slots = [.. g.Slots.Select(s => new DTMatchmakingSlot
                {
                    PlayerId = s.PlayerId,
                    RulerTitle = s.RulerTitle,
                    RulerName = s.RulerName,
                    FactionName = s.FactionName,
                    RulerLabel = s.RulerLabel,
                    IsClaimed = s.IsClaimed,
                    IsClaimedByCurrentUser = s.IsClaimedByCurrentUser,
                })]
            }).ToList() ?? []
        });
    }

    /// <summary>
    /// Claim an unassigned player slot for the logged-in user.
    /// </summary>
    /// <response code="204">Slot claimed.</response>
    /// <response code="400">Validation error</response>
    /// <response code="401">Missing or invalid token</response>
    /// <response code="404">The slot does not exist in that game</response>
    /// <response code="409">The slot is already taken, or the user already controls a player in that game</response>
    /// <response code="500">Returns an error response with an array of errors in the event of an exception or other server error</response>
    [HttpPost("claim")]
    [ProducesResponseType(204)]
    [ProducesResponseType(typeof(IApiErrorResult), 400)]
    [ProducesResponseType(401)]
    [ProducesResponseType(typeof(IApiErrorResult), 404)]
    [ProducesResponseType(typeof(IApiErrorResult), 409)]
    [ProducesResponseType(typeof(IApiErrorResult), 500)]
    public async Task<IActionResult> ClaimSlotAsync(
        [FromServices] IMediator mediator,
        [FromBody] ClaimPlayerSlotDto request,
        CancellationToken cancelToken)
    {
        int? userId = User.GetUserId();
        if (userId is null)
        {
            return Unauthorized();
        }

        // Treat the result as a bare status: the response carries no body, only the outcome code.
        ServiceResultBase response =
            await mediator.Send(new ClaimPlayerSlotRequest(userId.Value, request.GameId, request.PlayerId), cancelToken);

        return response.ToActionResult();
    }
}
