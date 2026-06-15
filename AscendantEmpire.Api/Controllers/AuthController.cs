using AscendantEmpire.Api.Authentication;
using AscendantEmpire.Api.Utilities;
using AscendantEmpire.Application.Features.Authentication.Classes;
using AscendantEmpire.Application.Features.Authentication.Handlers;
using AscendantEmpire.BackendContracts.Authentication;
using AscendantEmpire.FrontendContracts;
using AscendantEmpire.FrontendContracts.Authentication;

using MediatR;

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

using static AscendantEmpire.Application.Common.ServiceResultBase;

namespace AscendantEmpire.Api.Controllers;

[ApiController]
[Route("[controller]")]
public class AuthController : ControllerBase
{
    /// <summary>
    /// Register a new account.
    /// </summary>
    /// <response code="201">Account created; returns a user token.</response>
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
        [FromBody] RegisterDto request,
        CancellationToken cancelToken)
    {
        ServiceResult<AuthenticatedUser> response = await mediator.Send(new RegisterUserRequest(request.Username, request.Password, request.Email), cancelToken);

        // The mapping only runs on success, so the token is only built for a created account.
        return response.ToActionResult(user => new DTAuthTokenResult
        {
            Token = tokenService.CreateUserToken(user),
            Username = user.Username,
        });
    }

    /// <summary>
    /// Log in with a username and password.
    /// </summary>
    /// <response code="200">Logged in; returns a user token.</response>
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
        [FromBody] LoginDto request,
        CancellationToken cancelToken)
    {
        ServiceResult<AuthenticatedUser> response = await mediator.Send(new LoginRequest(request.Username, request.Password), cancelToken);

        return response.ToActionResult(user => new DTAuthTokenResult
        {
            Token = tokenService.CreateUserToken(user),
            Username = user.Username,
        });
    }

    /// <summary>
    /// Enter a specific game. Requires a valid user token; returns a game token that also
    /// carries the game and the player the user controls in it.
    /// </summary>
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
        [FromBody] EnterGameDto request,
        CancellationToken cancelToken)
    {
        // The user id comes from the proven token, never the request body.
        int? userId = User.GetUserId();
        if (userId is null)
        {
            return Unauthorized();
        }

        ServiceResult<GameContext> response = await mediator.Send(new EnterGameRequest(userId.Value, request.GameId), cancelToken);

        return response.ToActionResult(context => new DTAuthTokenResult
        {
            Token = tokenService.CreateGameToken(context),
            Username = context.Username,
            GameId = context.GameId,
            PlayerId = context.PlayerId,
        });
    }
}
