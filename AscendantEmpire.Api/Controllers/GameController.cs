using AscendantEmpire.Api.Utilities;
using AscendantEmpire.Application.Common;
using AscendantEmpire.Application.Handlers.Game;
using AscendantEmpire.BackendContracts.Game.AddGame;
using AscendantEmpire.FrontendContracts;

using MediatR;

using Microsoft.AspNetCore.Mvc;

namespace AscendantEmpire.Api.Controllers;

[ApiController]
[Route("[controller]")]
public class GameController : ControllerBase
{
    /// <summary>
    /// Add Game
    /// </summary>
    /// <response code="201">Game successfully created</response>
    /// <response code="400">Validation error</response>
    /// <response code="500">Returns an error response with an array of errors in the event of an exception or other server error</response>
    [HttpPost("setup")]
    [ProducesResponseType(201)]
    [ProducesResponseType(400)]
    [ProducesResponseType(typeof(IApiErrorResult), 500)]
    public async Task<IActionResult> SetupNewGameAsync(
        [FromServices] IMediator mediator,
        [FromBody] SetupGameDto request)
    {
        ServiceResultBase response = await mediator.Send(new SetupGameRequest(request.MapId, request.GameName, request.NumberOfPlayers));

        return response.ToActionResult();
    }
}
