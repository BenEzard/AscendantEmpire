using AscendantEmpire.Api.Utilities;
using AscendantEmpire.Application.Common;
using AscendantEmpire.Application.Features.TroopOrders.Handlers;
using AscendantEmpire.BackendContracts.TroopOrders.MoveTroops;
using AscendantEmpire.FrontendContracts;

using MediatR;

using Microsoft.AspNetCore.Mvc;

namespace AscendantEmpire.Api.Controllers;

[ApiController]
[Route("[controller]")]

public class TroopOrdersController : ControllerBase
{
    /// <summary>
    /// Order the troops to move to a specific location on the map.
    /// </summary>
    /// <response code="201">Game successfully created</response>
    /// <response code="400">Validation error</response>
    /// <response code="500">Returns an error response with an array of errors in the event of an exception or other server error</response>
    [HttpPost("move")]
    [ProducesResponseType(201)]
    [ProducesResponseType(typeof(IApiErrorResult), 400)]
    [ProducesResponseType(typeof(IApiErrorResult), 500)]
    public async Task<IActionResult> MoveTroopsAsync(
        [FromServices] IMediator mediator,
        [FromBody] MoveTroopsDto request)
    {
        ServiceResultBase response = await mediator.Send(new MoveTroopRequest(
            request.GameId,
            request.PlayerId, 
            request.TroopInstanceId, 
            request.XCoord,
            request.YCoord));

        return response.ToActionResult();
    }
}
