using AscendantEmpire.Api.Authentication;
using AscendantEmpire.Api.Utilities;
using AscendantEmpire.Application.Common;
using AscendantEmpire.Application.Features.Authentication.Classes;
using AscendantEmpire.Application.Features.Authentication.Handlers;
using AscendantEmpire.Application.Features.Authentication.Services;
using AscendantEmpire.BackendContracts.Authentication;
using AscendantEmpire.BackendContracts.Authentication.GetUsers;
using AscendantEmpire.Domain.Enums.Authentication;
using AscendantEmpire.FrontendContracts;
using AscendantEmpire.FrontendContracts.Authentication;

using MediatR;

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;

using static AscendantEmpire.Application.Common.ServiceResultBase;

namespace AscendantEmpire.Api.Controllers;

[ApiController]
[Route("[controller]")]
public class AuthController : ControllerBase
{
    /// <summary>
    /// Return the current auth users.
    /// </summary>
    /// <response code="200">Current auth users.</response>
    /// <response code="401">Missing or invalid token</response>
    /// <response code="403">The caller does not hold the System panel permission</response>
    /// <response code="500">Returns an error response with an array of errors in the event of an exception or other server error</response>
    [Authorize(Policy = nameof(PermissionType.SystemPanel))]
    [HttpGet("users")]
    [ProducesResponseType(typeof(IReadOnlyCollection<AuthUser>), 200)]
    [ProducesResponseType(401)]
    [ProducesResponseType(typeof(IApiErrorResult), 500)]
    public async Task<IActionResult> GetUsersAsync(
        [FromServices] IMediator mediator,
        CancellationToken cancelToken)
    {
        ServiceResult<IReadOnlyCollection<AuthUser>> response = await mediator.Send(new GetUsersRequest(), cancelToken);

        return response.ToActionResult();
    }

    /// <summary>
    /// Update an auth user's inactivity state.
    /// </summary>
    /// <response code="204">User inactivity state updated.</response>
    /// <response code="400">Validation error</response>
    /// <response code="401">Missing or invalid token</response>
    /// <response code="403">The caller does not hold the System panel permission</response>
    /// <response code="404">User not found</response>
    /// <response code="500">Returns an error response with an array of errors in the event of an exception or other server error</response>
    [Authorize(Policy = nameof(PermissionType.SystemPanel))]
    [HttpPost("users/inactive")]
    [ProducesResponseType(204)]
    [ProducesResponseType(typeof(IApiErrorResult), 400)]
    [ProducesResponseType(401)]
    [ProducesResponseType(typeof(IApiErrorResult), 403)]
    [ProducesResponseType(typeof(IApiErrorResult), 404)]
    [ProducesResponseType(typeof(IApiErrorResult), 500)]
    public async Task<IActionResult> SetUserInactiveAsync(
        [FromServices] IMediator mediator,
        [FromBody] SetUserInactiveDto request,
        CancellationToken cancelToken)
    {
        ServiceResultBase response = await mediator.Send(new SetUserInactiveRequest(request.UserId, request.InactiveReasonType), cancelToken);

        return response.ToActionResult();
    }

    /// <summary>
    /// Replace the set of permissions granted to a user.
    /// </summary>
    /// <remarks>
    /// The body carries the complete desired state, so any permission it omits is revoked. A changed permission reaches the affected user on their next
    /// token refresh rather than immediately — their current access token keeps whatever it was minted with until it expires.
    /// </remarks>
    /// <response code="204">Permissions updated.</response>
    /// <response code="400">Validation error</response>
    /// <response code="401">Missing or invalid token</response>
    /// <response code="403">The caller does not hold the System panel permission</response>
    /// <response code="404">User not found</response>
    /// <response code="500">Returns an error response with an array of errors in the event of an exception or other server error</response>
    [Authorize(Policy = nameof(PermissionType.SystemPanel))]
    [HttpPut("users/permissions")]
    [ProducesResponseType(204)]
    [ProducesResponseType(typeof(IApiErrorResult), 400)]
    [ProducesResponseType(401)]
    [ProducesResponseType(typeof(IApiErrorResult), 403)]
    [ProducesResponseType(typeof(IApiErrorResult), 404)]
    [ProducesResponseType(typeof(IApiErrorResult), 500)]
    public async Task<IActionResult> SetUserPermissionsAsync(
        [FromServices] IMediator mediator,
        [FromBody] SetUserPermissionsDto request,
        CancellationToken cancelToken)
    {
        ServiceResultBase response = await mediator.Send(new SetUserPermissionsRequest(request.UserId, request.Permissions), cancelToken);

        return response.ToActionResult();
    }

    /// <summary>
    /// Register a new account.
    /// </summary>
    /// <response code="201">Account created; returns access + refresh tokens.</response>
    /// <response code="400">Validation error</response>
    /// <response code="409">Username already taken</response>
    /// <response code="500">Returns an error response with an array of errors in the event of an exception or other server error</response>
    [AllowAnonymous]
    [HttpPost("register")]
    [ProducesResponseType(typeof(IAuthTokenResult), 201)]
    [ProducesResponseType(typeof(IApiErrorResult), 400)]
    [ProducesResponseType(typeof(IApiErrorResult), 409)]
    [ProducesResponseType(typeof(IApiErrorResult), 500)]
    public async Task<IActionResult> RegisterAsync(
        [FromServices] IMediator mediator,
        [FromServices] ITokenService tokenService,
        [FromServices] IRefreshSessionService sessionService,
        [FromServices] IOptions<JwtSettings> jwtOptions,
        [FromBody] RegisterDto request,
        CancellationToken cancelToken)
    {
        ServiceResult<AuthenticatedUser> response = await mediator.Send(new RegisterUserRequest(request.Username, request.Password, request.Email), cancelToken);

        return await IssueLoginResultAsync(response, tokenService, sessionService, jwtOptions.Value, cancelToken);
    }

    /// <summary>
    /// Log in with a username and password.
    /// </summary>
    /// <response code="200">Logged in; returns access + refresh tokens.</response>
    /// <response code="400">Validation error</response>
    /// <response code="403">Invalid username or password</response>
    /// <response code="500">Returns an error response with an array of errors in the event of an exception or other server error</response>
    [AllowAnonymous]
    [HttpPost("login")]
    [ProducesResponseType(typeof(IAuthTokenResult), 200)]
    [ProducesResponseType(typeof(IApiErrorResult), 400)]
    [ProducesResponseType(typeof(IApiErrorResult), 403)]
    [ProducesResponseType(typeof(IApiErrorResult), 500)]
    public async Task<IActionResult> LoginAsync(
        [FromServices] IMediator mediator,
        [FromServices] ITokenService tokenService,
        [FromServices] IRefreshSessionService sessionService,
        [FromServices] IOptions<JwtSettings> jwtOptions,
        [FromBody] LoginDto request,
        CancellationToken cancelToken)
    {
        ServiceResult<AuthenticatedUser> response = await mediator.Send(new LoginRequest(request.Username, request.Password), cancelToken);

        return await IssueLoginResultAsync(response, tokenService, sessionService, jwtOptions.Value, cancelToken);
    }

    /// <summary>
    /// Enter a specific game. 
    /// </summary>
    /// <remarks>
    /// Requires a valid user token; returns a game token that also carries the game and the player the user controls in it. 
    /// The refresh token is unchanged (the session is the same one), so it is not returned again.
    /// </remarks>
    /// <response code="200">Entered game; returns a game token.</response>
    /// <response code="400">Validation error</response>
    /// <response code="401">Missing or invalid token</response>
    /// <response code="403">The user is not a player in that game</response>
    /// <response code="500">Returns an error response with an array of errors in the event of an exception or other server error</response>
    [Authorize]
    [HttpPost("enter-game")]
    [ProducesResponseType(typeof(IAuthTokenResult), 200)]
    [ProducesResponseType(typeof(IApiErrorResult), 400)]
    [ProducesResponseType(401)]
    [ProducesResponseType(typeof(IApiErrorResult), 403)]
    [ProducesResponseType(typeof(IApiErrorResult), 500)]
    public async Task<IActionResult> EnterGameAsync(
        [FromServices] IMediator mediator,
        [FromServices] ITokenService tokenService,
        [FromServices] IRefreshSessionService sessionService,
        [FromBody] EnterGameDto request,
        CancellationToken cancelToken)
    {
        // The user and session both come from the proven token, never the request body.
        int? userId = User.GetUserId();
        int? sessionId = User.GetSessionId();
        if (userId is null || sessionId is null)
        {
            return Unauthorized();
        }

        ServiceResult<GameContext> response = await mediator.Send(new EnterGameRequest(userId.Value, request.GameId), cancelToken);

        if (response.HasSucceeded == false || response.SuccessValue is null)
        {
            return response.ToActionResult();
        }

        GameContext context = response.SuccessValue;

        // Record the entered game on the session so a later refresh re-issues a game token and so the session can be revoked by game (e.g. when the game is deleted).
        await sessionService.UpdateGameContextAsync(sessionId.Value, context.GameId, context.PlayerId, cancelToken);

        DTAuthTokenResult body = new()
        {
            Token = tokenService.CreateGameToken(context, sessionId.Value),
            Username = context.Username,
            GameId = context.GameId,
            PlayerId = context.PlayerId,
            Permissions = context.Permissions,
        };

        return response.ToActionResult(_ => body);
    }

    /// <summary>
    /// Exchange a refresh token for a fresh access token (and a rotated refresh token).
    /// </summary>
    /// <remarks>The new access token is scoped to whatever game the session is currently in.</remarks>
    /// <response code="200">Token refreshed; returns access + refresh tokens.</response>
    /// <response code="401">The refresh token is unknown, revoked or expired.</response>
    [AllowAnonymous]
    [HttpPost("refresh")]
    [ProducesResponseType(typeof(IAuthTokenResult), 200)]
    [ProducesResponseType(401)]
    public async Task<IActionResult> RefreshAsync(
        [FromServices] ITokenService tokenService,
        [FromServices] IRefreshSessionService sessionService,
        [FromBody] RefreshTokenDto request,
        CancellationToken cancelToken)
    {
        RefreshedSession? refreshed = await sessionService.RotateAsync(request.RefreshToken, cancelToken);

        if (refreshed is null)
        {
            return Unauthorized();
        }

        // A session that has entered a game gets a game-scoped token; otherwise a plain user token.
        // Either way the permissions come from the rotation, which re-read them from the database.
        string token;
        if (refreshed.GameId is int gameId && refreshed.PlayerId is int playerId)
        {
            GameContext context = new(refreshed.UserId, refreshed.Username, gameId, playerId, refreshed.Permissions);
            token = tokenService.CreateGameToken(context, refreshed.SessionId);
        }
        else
        {
            AuthenticatedUser user = new(refreshed.UserId, refreshed.Username, refreshed.Permissions);
            token = tokenService.CreateUserToken(user, refreshed.SessionId);
        }

        DTAuthTokenResult body = new()
        {
            Token = token,
            RefreshToken = refreshed.RefreshToken,
            Username = refreshed.Username,
            GameId = refreshed.GameId,
            PlayerId = refreshed.PlayerId,
            Permissions = refreshed.Permissions,
        };

        return Ok(body);
    }

    /// <summary>
    /// Log out by revoking the session behind the current token.
    /// </summary>
    /// <remarks>The access token still works until it expires, but it can no longer be refreshed.</remarks>
    /// <response code="204">Logged out (or already logged out).</response>
    /// <response code="401">Missing or invalid token</response>
    [Authorize]
    [HttpPost("logout")]
    [ProducesResponseType(204)]
    [ProducesResponseType(401)]
    public async Task<IActionResult> LogoutAsync(
        [FromServices] IRefreshSessionService sessionService,
        CancellationToken cancelToken)
    {
        int? sessionId = User.GetSessionId();
        if (sessionId is not null)
        {
            await sessionService.RevokeAsync(sessionId.Value, cancelToken);
        }

        return NoContent();
    }

    /// <summary>
    /// Shared tail for register and login: on a successful authentication, open a session and return a user-scoped access token plus the raw refresh token; on failure, 
    /// return the handler's error result unchanged.
    /// </summary>
    private static async Task<IActionResult> IssueLoginResultAsync(
        ServiceResult<AuthenticatedUser> response,
        ITokenService tokenService,
        IRefreshSessionService sessionService,
        JwtSettings jwtSettings,
        CancellationToken cancelToken)
    {
        if (response.HasSucceeded == false || response.SuccessValue is null)
        {
            // The identity mapping is never invoked on error statuses, so only the error body is produced.
            return response.ToActionResult();
        }

        AuthenticatedUser user = response.SuccessValue;

        DateTimeOffset expiresAt = DateTimeOffset.UtcNow.AddDays(jwtSettings.RefreshTokenDays);
        IssuedSession issued = await sessionService.IssueAsync(user.UserId, gameId: null, playerId: null, expiresAt, cancelToken);

        DTAuthTokenResult body = new()
        {
            Token = tokenService.CreateUserToken(user, issued.SessionId),
            RefreshToken = issued.RefreshToken,
            Username = user.Username,
            Permissions = user.Permissions,
        };

        // Preserve the handler's status code (201 Created for register, 200 OK for login).
        return response.ToActionResult(_ => body);
    }
}
