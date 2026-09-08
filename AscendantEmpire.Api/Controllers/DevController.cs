using AscendantEmpire.Api.Authentication;
using AscendantEmpire.Api.Utilities;
using AscendantEmpire.Application.Common.Messaging;
using AscendantEmpire.Application.Features.Authentication.Classes;
using AscendantEmpire.Application.Features.Authentication.Services;
using AscendantEmpire.Application.Features.Turns.Handlers;
using AscendantEmpire.Domain.Enums.Authentication;
using AscendantEmpire.FrontendContracts;

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;

using static AscendantEmpire.Application.Common.ServiceResultBase;

namespace AscendantEmpire.Api.Controllers;

// Remove in F-18. The whole controller, its result type below, and the endpoint the frontend Dev Tools buttons call, all go when F-18 is done.

/// <summary>
/// Development-only conveniences for playing a multi-player Game single-handed.
/// </summary>
/// <remarks>
/// <para>Remove in F-18. <see cref="DevelopmentOnlyAttribute"/> keeps these routes out of every environment but Development — outside it the controller is
/// dropped from the application model entirely, so the paths simply do not exist.</para>
/// <para>That guard is doing real work rather than being belt-and-braces: the endpoint below issues a session for another user's account without any
/// credential check at all. It is a play-testing shortcut and nothing else.</para>
/// </remarks>
[ApiController]
[Route("[controller]")]
[DevelopmentOnly]
// A game token is still required. The Turn that ends is the caller's own, read from the token's claims, so this cannot be pointed at somebody else's Game.
[Authorize]
public class DevController : ControllerBase
{
    /// <summary>
    /// End the caller's Campaign Turn, log them out, and log in as whoever plays next.
    /// </summary>
    /// <remarks>
    /// <para>Remove in F-18. Collapses the whole hot-seat sequence — end turn, close game, log out, log the next Player in, reopen the game — into one
    /// call, so that one developer can play every seat without knowing anyone else's password.</para>
    /// <para>With <paramref name="cycleWholeRound"/> the Turns of every Player in between are ended too, stopping when it is the caller's Turn again:
    /// "end round, back to this Player". Those Players are never logged in — only the Player the caller lands on is.</para>
    /// <para>The caller's own session is revoked before the new one is issued, so the token they arrived with cannot be refreshed afterwards. The response
    /// carries a fresh access and refresh token for the new Player along with the board detail needed to reopen the Game as them.</para>
    /// </remarks>
    /// <param name="mediator">Mediator used to dispatch the end-turn-and-switch request.</param>
    /// <param name="tokenService">Mints the game token for the Player being switched to.</param>
    /// <param name="sessionService">Revokes the caller's session and issues the new Player's.</param>
    /// <param name="jwtOptions">Supplies the refresh token lifetime for the new session.</param>
    /// <param name="cancelToken">Cancellation token.</param>
    /// <param name="cycleWholeRound">End every Turn until it is the caller's Turn again, rather than stopping at the next Player. Defaults to false.</param>
    /// <response code="200">The Turn(s) ended; returns the new Player's tokens and the Game detail to reopen with</response>
    /// <response code="400">Returns an error response with an array of errors in the event of a failed request due to client error, such as invalid input</response>
    /// <response code="401">Missing or invalid token</response>
    /// <response code="403">The caller has not entered a game</response>
    /// <response code="404">Returns an error response when a Game, Player or TurnQueue not found</response>
    /// <response code="500">Returns an error response with an array of errors in the event of an exception or other server error</response>
    [HttpPost("end-turn-and-switch-player")]
    [ProducesResponseType(typeof(DTDevSwitchPlayerResult), 200)]
    [ProducesResponseType(typeof(IApiErrorResult), 400)]
    [ProducesResponseType(401)]
    [ProducesResponseType(typeof(IApiErrorResult), 403)]
    [ProducesResponseType(typeof(IApiErrorResult), 404)]
    [ProducesResponseType(typeof(IApiErrorResult), 500)]
    public async Task<IActionResult> EndTurnAndSwitchPlayerAsync(
        [FromServices] IMediator mediator,
        [FromServices] ITokenService tokenService,
        [FromServices] IRefreshSessionService sessionService,
        [FromServices] IOptions<JwtSettings> jwtOptions,
        CancellationToken cancelToken,
        [FromQuery] bool cycleWholeRound = false)
    {
        // Game and Player come from the token, exactly as they do on the ordinary end-turn endpoint. Nothing in the request says whose Turn ends.
        if (!this.TryGetGameContext(out int gameId, out int playerId, out IActionResult? contextError))
        {
            return contextError!;
        }

        int? callerSessionId = User.GetSessionId();

        ServiceResult<DevSwitchedPlayer> response = await mediator.Send(
            new DevEndTurnAndSwitchPlayerRequest(gameId, playerId, cycleWholeRound),
            cancelToken);

        if (response.HasSucceeded == false || response.SuccessValue is null)
        {
            // Nothing has been swapped, so the caller is left logged in as they were and can read the error and try again.
            return response.ToActionResult();
        }

        DevSwitchedPlayer switched = response.SuccessValue;

        // Log the caller out. Deliberately after the Turn work rather than before it: a failure above leaves them with a working session instead of
        // stranding them logged out of a Game whose Turn never ended.
        if (callerSessionId is not null)
        {
            await sessionService.RevokeAsync(callerSessionId.Value, cancelToken);
        }

        // Log the next Player in. The session records the Game and Player from the outset, so a later refresh re-issues a game token rather than dropping
        // the caller back to a user token mid-turn.
        GameContext context = switched.GameContext;
        DateTimeOffset expiresAt = DateTimeOffset.UtcNow.AddDays(jwtOptions.Value.RefreshTokenDays);

        IssuedSession issued = await sessionService.IssueAsync(context.UserId, context.GameId, context.PlayerId, expiresAt, cancelToken);

        DTDevSwitchPlayerResult body = new()
        {
            Token = tokenService.CreateGameToken(context, issued.SessionId),
            RefreshToken = issued.RefreshToken,
            Username = context.Username,
            GameId = context.GameId,
            PlayerId = context.PlayerId,
            Permissions = context.Permissions,

            GameName = switched.GameName,
            MapWidth = switched.MapWidth,
            MapHeight = switched.MapHeight,
            RulerLabel = switched.RulerLabel,
            CampaignOffsetId = switched.CampaignOffsetId,

            TurnsEnded = switched.TurnsEnded,
            RoundEnded = switched.RoundEnded,
        };

        return Ok(body);
    }
}

/// <summary>
/// The new Player's tokens, plus everything the client needs to redraw the board as them.
/// </summary>
/// <remarks>
/// <para>Remove in F-18. Deliberately declared here rather than in the contracts projects: it is temporary, so keeping it beside the only endpoint that
/// returns it means the removal is a file delete rather than a hunt.</para>
/// <para>The token half mirrors the auth endpoints' result. The Game half is what the Open Game dialog would normally have supplied off the row that was
/// clicked, which this switch never gives the client the chance to read.</para>
/// </remarks>
public class DTDevSwitchPlayerResult
{
    public required string Token { get; init; }
    public required string RefreshToken { get; init; }
    public required string Username { get; init; }
    public required int GameId { get; init; }
    public required int PlayerId { get; init; }
    public required IReadOnlyCollection<PermissionType> Permissions { get; init; }

    public required string GameName { get; init; }
    public required int MapWidth { get; init; }
    public required int MapHeight { get; init; }

    /// <summary>
    /// The new Player's ruler label, in the "Title Name (Faction)" shape the Open Game listing uses.
    /// </summary>
    public required string RulerLabel { get; init; }

    public required int CampaignOffsetId { get; init; }

    /// <summary>
    /// How many Turns were ended to get here: one for a single step, one per Player for a whole Round.
    /// </summary>
    public required int TurnsEnded { get; init; }

    /// <summary>
    /// True when at least one of those Turns closed the Round.
    /// </summary>
    public required bool RoundEnded { get; init; }
}
