using AscendantEmpire.Api.Utilities;
using AscendantEmpire.Application.Common;
using AscendantEmpire.Application.Features.Units.Classes;
using AscendantEmpire.Application.Features.Units.Handlers;
using AscendantEmpire.BackendContracts.Units.GetTroopDisplayInfo;
using AscendantEmpire.BackendContracts.Units.Spawn;
using AscendantEmpire.FrontendContracts;
using AscendantEmpire.FrontendContracts.Units.GetTroopDisplayInfo;

using MediatR;

using Microsoft.AspNetCore.Mvc;

using static AscendantEmpire.Application.Common.ServiceResultBase;

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
        ServiceResultBase response = await mediator.Send(new SpawnTroopRequest(request.GameId, 
            request.PlayerId, 
            request.XCoord, 
            request.YCoord, 
            request.UnitDesignId,
            request.InitialStrength,
            request.HealthyCount,
            request.InjuredCount),
            cancelToken);

        return response.ToActionResult();
    }

    /// <summary>
    /// Get the UnitDesign display information which maps the UnitDesignId to the image which should be shown on the map.
    /// </summary>
    /// <response code="201">End Turn</response>
    /// <response code="500">Returns an error response with an array of errors in the event of an exception or other server error</response>
    [HttpGet("{gameId}/display")]
    [ProducesResponseType(typeof(ITroopDisplayInfoCollection), 200)]
    [ProducesResponseType(typeof(IApiErrorResult), 500)]
    public async Task<IActionResult> GetUnitsAsync(
        [FromServices] IMediator mediator,
        [FromRoute] int gameId,
        CancellationToken cancelToken)
    {
        ServiceResult<IReadOnlyCollection<TroopDisplayInfo>> response = await mediator.Send(new GetTroopDisplayInfoRequest(gameId), cancelToken);

        return response.ToActionResult(contract =>
        {
            return new DTTroopDisplayInfoCollection
            {
                TroopDisplayInfo = contract.Select(t => new DTTroopDisplayInfo
                {
                    UnitDesignId = t.UnitDesignId,
                    MapImagePath = t.MapImagePath
                }).ToList() ?? []
            };
        });
    }
}
