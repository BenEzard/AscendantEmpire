using AscendantEmpire.Api.Authentication;
using AscendantEmpire.Api.Utilities;
using AscendantEmpire.Application.Common;
using AscendantEmpire.Application.Common.Messaging;
using AscendantEmpire.Application.Features.Authentication.Classes;
using AscendantEmpire.Application.Features.Authentication.Services;
using AscendantEmpire.Application.Features.Assets.Handlers;
using AscendantEmpire.Application.Features.Turns.Handlers;
using AscendantEmpire.Domain.Enums.Authentication;
using AscendantEmpire.FrontendContracts;

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;

using static AscendantEmpire.Application.Common.ServiceResultBase;

namespace AscendantEmpire.Api.Controllers;

/// <summary>
/// Development-only conveniences standing in for systems that do not exist yet.
/// </summary>
/// <remarks>
/// <para><see cref="DevelopmentOnlyAttribute"/> keeps these routes out of every environment but Development — outside it the controller is dropped from the
/// application model entirely, so the paths simply do not exist.</para>
/// <para>That guard is doing real work rather than being belt-and-braces: the hot-seat endpoint issues a session for another user's account without any
/// credential check at all. These are play-testing shortcuts and nothing else.</para>
/// <para>
/// The actions have <b>different lifetimes</b>, so this file is not a wholesale delete:
/// <list type="bullet">
///   <item><description><c>end-turn-and-switch-player</c> and <see cref="DTDevSwitchPlayerResult"/> go in F-18, with the frontend Dev Tools buttons that call them.</description></item>
///   <item><description>the <c>player-assets</c> actions and their request types go when the Technology system takes over the unlock rows.</description></item>
/// </list>
/// </para>
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

    // ---------------------------------------------------------------------------------------------------------------------------------------------------
    // Asset unlocks. Unlike the hot-seat endpoint above, this one is NOT removed in F-18 — it stands in for the Technology system and goes when that
    // lands. Whoever does F-18 should keep this action (and the controller) rather than deleting the file wholesale.
    // ---------------------------------------------------------------------------------------------------------------------------------------------------

    /// <summary>
    /// Grant the calling Player the named Assets and AssetMetaCategories.
    /// </summary>
    /// <remarks>
    /// Stands in for the Technology system, which will eventually own these rows. Without it the unlock path cannot be exercised by hand at all: a new game
    /// grants its starter set and nothing ever changes it, so a meta-category icon appearing on unlock could never be seen.
    /// </remarks>
    /// <response code="204">The unlocks were granted</response>
    /// <response code="400">No codes were supplied</response>
    /// <response code="403">The caller has not entered a game</response>
    /// <response code="404">A supplied code matches no catalogue row, or the Game has no campaign turn queue</response>
    /// <response code="500">Returns an error response with an array of errors in the event of an exception or other server error</response>
    [HttpPut("player-assets/grant")]
    [ProducesResponseType(204)]
    [ProducesResponseType(typeof(IApiErrorResult), 400)]
    [ProducesResponseType(401)]
    [ProducesResponseType(typeof(IApiErrorResult), 403)]
    [ProducesResponseType(typeof(IApiErrorResult), 404)]
    [ProducesResponseType(typeof(IApiErrorResult), 500)]
    public async Task<IActionResult> GrantPlayerAssetsAsync(
        [FromServices] IMediator mediator,
        [FromBody] DTDevGrantAssetRequest body,
        CancellationToken cancelToken)
    {
        // Game and Player come from the token, so this cannot be pointed at somebody else's unlocks.
        if (!this.TryGetGameContext(out int gameId, out int playerId, out IActionResult? contextError))
        {
            return contextError!;
        }

        ServiceResultBase response = await mediator.Send(
            new GrantPlayerAssetRequest(gameId, playerId, body.AssetCodes ?? [], body.MetaCategoryCodes ?? [], body.ObsoleteInRounds),
            cancelToken);

        return response.ToActionResult();
    }

    /// <summary>
    /// Take the named Assets and AssetMetaCategories back off the calling Player.
    /// </summary>
    /// <remarks>
    /// Deletes the rows rather than obsoleting them — no row means never granted, which is what undoing a grant wants. To retire something the Player has kept
    /// instead, grant it with <c>obsoleteInRounds</c> set.
    /// </remarks>
    /// <response code="204">The unlocks were withdrawn</response>
    /// <response code="400">No codes were supplied</response>
    /// <response code="403">The caller has not entered a game</response>
    /// <response code="404">A supplied code matches no catalogue row</response>
    /// <response code="500">Returns an error response with an array of errors in the event of an exception or other server error</response>
    [HttpPut("player-assets/withdraw")]
    [ProducesResponseType(204)]
    [ProducesResponseType(typeof(IApiErrorResult), 400)]
    [ProducesResponseType(401)]
    [ProducesResponseType(typeof(IApiErrorResult), 403)]
    [ProducesResponseType(typeof(IApiErrorResult), 404)]
    [ProducesResponseType(typeof(IApiErrorResult), 500)]
    public async Task<IActionResult> WithdrawPlayerAssetsAsync(
        [FromServices] IMediator mediator,
        [FromBody] DTDevWithdrawAssetRequest body,
        CancellationToken cancelToken)
    {
        if (!this.TryGetGameContext(out int gameId, out int playerId, out IActionResult? contextError))
        {
            return contextError!;
        }

        ServiceResultBase response = await mediator.Send(
            new WithdrawPlayerAssetRequest(gameId, playerId, body.AssetCodes ?? [], body.MetaCategoryCodes ?? []),
            cancelToken);

        return response.ToActionResult();
    }
}

/// <summary>
/// Which Assets and AssetMetaCategories to grant, and whether they are to start obsoleting.
/// </summary>
/// <remarks>
/// Remove with the Technology system, alongside the endpoint that takes it. Declared here rather than in the contracts projects for the same reason as
/// <see cref="DTDevSwitchPlayerResult"/>: it is temporary, so keeping it beside its only endpoint makes the removal a delete rather than a hunt.
/// </remarks>
public class DTDevGrantAssetRequest
{
    /// <summary>
    /// Catalogue codes of the Assets to grant, e.g. "Weapons.MeleeWeapons.MedievalSword.Low".
    /// </summary>
    public IReadOnlyCollection<string>? AssetCodes { get; init; }

    /// <summary>
    /// Catalogue codes of the AssetMetaCategories to grant, e.g. "ConstructionMaterials".
    /// </summary>
    public IReadOnlyCollection<string>? MetaCategoryCodes { get; init; }

    /// <summary>
    /// Schedules the granted rows to stop being available this many Rounds from now, so the obsoleting countdown can be exercised.
    /// </summary>
    /// <remarks>Null leaves them current indefinitely.</remarks>
    public int? ObsoleteInRounds { get; init; }
}

/// <summary>
/// Which Assets and AssetMetaCategories to take back off the Player.
/// </summary>
/// <remarks>Remove with the Technology system, alongside the endpoint that takes it.</remarks>
public class DTDevWithdrawAssetRequest
{
    /// <summary>
    /// Catalogue codes of the Assets to withdraw.
    /// </summary>
    public IReadOnlyCollection<string>? AssetCodes { get; init; }

    /// <summary>
    /// Catalogue codes of the AssetMetaCategories to withdraw.
    /// </summary>
    public IReadOnlyCollection<string>? MetaCategoryCodes { get; init; }
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
