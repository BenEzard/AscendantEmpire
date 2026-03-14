using AscendantEmpire.Api.Utilities;
using AscendantEmpire.Application.Common;
using AscendantEmpire.Application.Handlers.Units;
using AscendantEmpire.BackendContracts.Units.Spawn;
using AscendantEmpire.FrontendContracts;

using MediatR;

using Microsoft.AspNetCore.Mvc;

namespace AscendantEmpire.Api.Controllers;

[ApiController]
[Route("[controller]")]
public class UnitsController : ControllerBase
{
    /// <summary>
    /// Spawn a troop into a game and location.
    /// </summary>
    /// <response code="201">End Turn</response>
    /// <response code="500">Returns an error response with an array of errors in the event of an exception or other server error</response>
    [HttpPost("temporary-dev-only/spawn")]
    [ProducesResponseType(201)]
    [ProducesResponseType(typeof(IApiErrorResult), 500)]
    public async Task<IActionResult> SpawnTroopAsync(
        [FromServices] IMediator mediator,
        [FromBody] SpawnUnitDto request,
        CancellationToken cancelToken)
    {
        ServiceResultBase response = await mediator.Send(new SpawnTroopRequest(request.GameGuid, 
            request.PlayerGuid, 
            request.XCoord, 
            request.YCoord, 
            request.UnitDesignId), 
            cancelToken);

        return response.ToActionResult();
    }
}
