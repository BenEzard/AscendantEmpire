using AscendantEmpire.Api.Utilities;
using AscendantEmpire.Application.Common;
using AscendantEmpire.Application.Common.Messaging;
using AscendantEmpire.Application.Features.MatchMaking.Classes;
using AscendantEmpire.Application.Features.MatchMaking.Handlers;
using AscendantEmpire.BackendContracts.MatchMaking;
using AscendantEmpire.Domain.Enums.Authentication;
using AscendantEmpire.FrontendContracts;
using AscendantEmpire.FrontendContracts.MatchMaking;

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
public class MatchMakingController : ControllerBase
{
    /// <summary>
    /// List the games that can be joined, with each active slot flagged as claimed or free.
    /// </summary>
    /// <response code="200">The list of joinable games and their slots.</response>
    /// <response code="401">Missing or invalid token</response>
    /// <response code="500">Returns an error response with an array of errors in the event of an exception or other server error</response>
    [HttpGet("games")]
    [ProducesResponseType(typeof(IMatchMakingGameCollection), 200)]
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

        ServiceResult<IReadOnlyCollection<MatchMakingGame>> response =
            await mediator.Send(new GetMatchMakingGamesRequest(userId.Value), cancelToken);

        return response.ToActionResult(games => new DTMatchMakingGameCollection
        {
            Games = response.SuccessValue?.Select(g => new DTMatchMakingGame
            {
                GameId = g.GameId,
                GameName = g.GameName,
                NumberOfPlayers = g.NumberOfPlayers,
                MapWidth = g.MapWidth,
                MapHeight = g.MapHeight,
                HasStarted = g.HasStarted,
                Slots = [.. g.Slots.Select(s => new DTMatchMakingSlot
                {
                    PlayerId = s.PlayerId,
                    RulerTitle = s.RulerTitle,
                    RulerName = s.RulerName,
                    FactionName = s.FactionName,
                    FactionColour = s.FactionColour,
                    RulerLabel = s.RulerLabel,
                    IsClaimed = s.IsClaimed,
                    IsClaimedByCurrentUser = s.IsClaimedByCurrentUser,
                    IsDefined = s.IsDefined,
                })]
            }).ToList() ?? []
        });
    }

    /// <summary>
    /// Claim an unassigned player slot for the logged-in user.
    /// </summary>
    /// <remarks>
    /// Claiming a seat no longer starts the game, not even the last free one - the player still has to define themselves in it. See the PUT below.
    /// </remarks>
    /// <response code="204">Slot claimed.</response>
    /// <response code="400">Validation error</response>
    /// <response code="401">Missing or invalid token</response>
    /// <response code="404">The slot does not exist in that game</response>
    /// <response code="409">The slot is already taken, or the user already controls a player in that game</response>
    /// <response code="500">Returns an error response with an array of errors in the event of an exception or other server error</response>
    [Authorize(Policy = nameof(PermissionType.JoinGame))]
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

    /// <summary>
    /// Read back the caller's own ruler and faction identity in a game, plus the colours their rivals hold.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The route names the game, never the player: the slot within it is resolved from the token's user id, so this cannot describe anyone else.
    /// </para>
    /// <para>
    /// Reached while the caller still holds a plain user token - they have not entered the game yet - so the account comes from
    /// <c>User.GetUserId()</c> rather than from a game context.
    /// </para>
    /// </remarks>
    /// <param name="gameId">The game whose slot the caller wants to read.</param>
    /// <response code="200">The caller's player definition.</response>
    /// <response code="400">Validation error</response>
    /// <response code="401">Missing or invalid token</response>
    /// <response code="404">The caller does not control a player in that game</response>
    /// <response code="500">Returns an error response with an array of errors in the event of an exception or other server error</response>
    [HttpGet("games/{gameId:int}/my-player")]
    [ProducesResponseType(typeof(IPlayerDefinition), 200)]
    [ProducesResponseType(typeof(IApiErrorResult), 400)]
    [ProducesResponseType(401)]
    [ProducesResponseType(typeof(IApiErrorResult), 404)]
    [ProducesResponseType(typeof(IApiErrorResult), 500)]
    public async Task<IActionResult> GetMyPlayerAsync(
        [FromServices] IMediator mediator,
        int gameId,
        CancellationToken cancelToken)
    {
        int? userId = User.GetUserId();
        if (userId is null)
        {
            return Unauthorized();
        }

        ServiceResult<PlayerDefinition> response =
            await mediator.Send(new GetPlayerDefinitionRequest(userId.Value, gameId), cancelToken);

        return response.ToActionResult(definition => new DTPlayerDefinition
        {
            PlayerId = definition.PlayerId,
            GameId = definition.GameId,
            RulerTitle = definition.RulerTitle,
            RulerName = definition.RulerName,
            RulerGender = definition.RulerGender,
            FactionName = definition.FactionName,
            FactionColour = definition.FactionColour,
            PeopleName = definition.PeopleName,
            Demonym = definition.Demonym,
            CultureName = definition.CultureName,
            Language = definition.Language,
            RulerPortraitPath = definition.RulerPortraitPath,
            UsedRulerPortraits = [.. definition.UsedRulerPortraits.Select(p => new DTUsedRulerPortrait
            {
                Value = p.Value,
                UsedBy = p.UsedBy
            })],
            UsedFactionColours = [.. definition.UsedFactionColours.Select(c => new DTUsedFactionColour
            {
                Value = c.Value,
                UsedBy = c.UsedBy
            })],
            IsLocked = definition.IsLocked
        });
    }

    /// <summary>
    /// Define the caller's own ruler and faction in a game. A full replacement of all ten values, not a patch.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The body carries no identity at all - the game comes from the route and the slot from the token - so it cannot name a slot the caller does
    /// not own. A caller who controls no slot in the game gets a 404, since there is nothing there to forbid them from.
    /// </para>
    /// <para>
    /// Six of the values are unique within the game: the five names the player authors, plus the faction colour and, once chosen, the ruler portrait.
    /// A clash answers 409 carrying a <see cref="PlayerDefinitionConflict"/> that names every clashing field at once, so a caller does not have to
    /// resubmit once per taken value. The language is deliberately exempt.
    /// </para>
    /// <para>
    /// Once the game is under way the definition is locked and only the ruler title is read out of the body; the other values are ignored rather than
    /// refused, because the client sends the whole object back. <c>IsLocked</c> on the GET above says when that is so.
    /// </para>
    /// <para>
    /// Saving the <i>last</i> outstanding definition in a game also starts it.
    /// </para>
    /// </remarks>
    /// <param name="gameId">The game whose slot the caller wants to define.</param>
    /// <response code="204">Player definition saved.</response>
    /// <response code="400">Validation error</response>
    /// <response code="401">Missing or invalid token</response>
    /// <response code="404">The caller does not control a player in that game</response>
    /// <response code="409">Another player in that game already holds one or more of the requested values</response>
    /// <response code="500">Returns an error response with an array of errors in the event of an exception or other server error</response>
    [Authorize(Policy = nameof(PermissionType.JoinGame))]
    [HttpPut("games/{gameId:int}/my-player")]
    [ProducesResponseType(204)]
    [ProducesResponseType(typeof(IApiErrorResult), 400)]
    [ProducesResponseType(401)]
    [ProducesResponseType(typeof(IApiErrorResult), 404)]
    [ProducesResponseType(typeof(PlayerDefinitionConflict), 409)]
    [ProducesResponseType(typeof(IApiErrorResult), 500)]
    public async Task<IActionResult> UpdateMyPlayerAsync(
        [FromServices] IMediator mediator,
        int gameId,
        [FromBody] UpdatePlayerDefinitionDto request,
        CancellationToken cancelToken)
    {
        int? userId = User.GetUserId();
        if (userId is null)
        {
            return Unauthorized();
        }

        ServiceResultBase response = await mediator.Send(
            new UpdatePlayerDefinitionRequest(
                UserId: userId.Value,
                GameId: gameId,
                RulerTitle: request.RulerTitle,
                RulerName: request.RulerName,
                RulerGender: request.RulerGender,
                FactionName: request.FactionName,
                FactionColour: request.FactionColour,
                PeopleName: request.PeopleName,
                Demonym: request.Demonym,
                CultureName: request.CultureName,
                Language: request.Language,
                RulerPortraitPath: request.RulerPortraitPath),
            cancelToken);

        return response.ToActionResult();
    }
}
