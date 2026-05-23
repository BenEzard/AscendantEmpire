using AscendantEmpire.Api.Utilities;
using AscendantEmpire.Application.Common;
using AscendantEmpire.Application.Features.Turns.Handlers;
using AscendantEmpire.Application.Features.Turns.Services;
using AscendantEmpire.BackendContracts.Turns.EndPlayerTurn;
using AscendantEmpire.BackendContracts.Turns.GetCurrentCampaignTurn;
using AscendantEmpire.Domain.Enums.Turns;
using AscendantEmpire.FrontendContracts;

using MediatR;

using Microsoft.AspNetCore.Mvc;

using static AscendantEmpire.Application.Common.ServiceResultBase;

namespace AscendantEmpire.Api.Controllers;

[ApiController]
[Route("[controller]")]
public class TurnController : ControllerBase
{
    /// <summary>
    /// Get a Player's position in the Turn Roster for the current Game
    /// </summary>
    /// <response code="200">Turn information for Campaign and Battle Maps</response>
    /// <response code="500">Returns an error response with an array of errors in the event of an exception or other server error</response>
    [HttpGet("/campaign")]
    [ProducesResponseType(typeof(CurrentCampaignTurn), 200)]
    [ProducesResponseType(typeof(IApiErrorResult), 500)]
    public async Task<IActionResult> GetPlayerTurnAsync(
        [FromServices] ICampaignTurnService campaignTurnService,
        [FromQuery] int gameId,
        CancellationToken cancelToken)
    {
        ServiceResult<CurrentCampaignTurn> response = await campaignTurnService.GetCurrentCampaignTurnAsync(gameId, cancelToken);

        return response.ToActionResult();
    }

    /// <summary>
    /// End a Player's Campaign Turn
    /// </summary>
    /// <response code="200">End Turn</response>
    /// <response code="500">Returns an error response with an array of errors in the event of an exception or other server error</response>
    [HttpPost("end-campaign")]
    [ProducesResponseType(204)]
    [ProducesResponseType(typeof(IApiErrorResult), 500)]
    public async Task<IActionResult> EndPlayerTurnAsync(
        [FromServices] IMediator mediator,
        [FromBody] EndPlayerTurnDto request,
        CancellationToken cancelToken)
    {
        ServiceResultBase response = await mediator.Send(new EndCampaignTurnRequest(request.GameId, request.PlayerId, EndTurnMode.CheckForActions), cancelToken);

        return response.ToActionResult();
    }
}
