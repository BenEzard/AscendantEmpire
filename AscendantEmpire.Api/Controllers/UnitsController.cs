using AscendantEmpire.Api.Utilities;
using AscendantEmpire.Application.Common;
using AscendantEmpire.Application.Handlers.Units;
using AscendantEmpire.BackendContracts.Units.Train;
using AscendantEmpire.FrontendContracts;

using MediatR;

using Microsoft.AspNetCore.Mvc;

namespace AscendantEmpire.Api.Controllers;

[ApiController]
[Route("[controller]")]
public class UnitsController : ControllerBase
{
    /// <summary>
    /// End a Player's Turn
    /// </summary>
    /// <response code="200">End Turn</response>
    /// <response code="500">Returns an error response with an array of errors in the event of an exception or other server error</response>
    [HttpPost("train")]
    [ProducesResponseType(201)]
    [ProducesResponseType(typeof(IApiErrorResult), 500)]
    public async Task<IActionResult> TrainTroopAsync(
        [FromServices] IMediator mediator,
        [FromBody] TrainUnitDto request,
        CancellationToken cancelToken)
    {
        //ServiceResultBase response = await mediator.Send(new TrainTroopRequest(request.GameGuid, request.PlayerGuid), cancelToken);

        //return response.ToActionResult();

        return null;
    }
}
