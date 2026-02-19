using AscendantEmpire.Api.Utilities;
using AscendantEmpire.Application.Common;
using AscendantEmpire.Application.Handlers.Turns;
using AscendantEmpire.Application.Handlers.Turns.Classes;
using AscendantEmpire.BackendContracts.Turns.EndPlayerTurn;
using AscendantEmpire.BackendContracts.Turns.GetPlayerTurn;
using AscendantEmpire.FrontendContracts;
using AscendantEmpire.FrontendContracts.Turns.GetTurns;

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
    [HttpGet("")]
    [ProducesResponseType(typeof(IRosterEntryCollection), 200)]
    [ProducesResponseType(typeof(IApiErrorResult), 500)]
    public async Task<IActionResult> GetPlayerTurnAsync(
        [FromServices] IMediator mediator,
        [FromQuery] Guid gameGuid,
        [FromQuery] Guid playerGuid,
        CancellationToken cancelToken)
    {
        ServiceResult<IReadOnlyCollection<RosterEntry>> response = await mediator.Send(new GetTurnRequest(gameGuid, playerGuid), cancelToken);

        return response.ToActionResult(contract =>
        {
            return new DTRosterEntryCollection
            {
                RosterEntries = response.SuccessValue?.Select(g => new DTRosterEntry
                {
                    CurrentTurnPlayerId = g.CurrentTurnPlayerId,
                    NumberOfPlayersUntilMyTurn = g.NumberOfPlayersUntilMyTurn,
                    IsCampaign = g.IsCampaign,
                    MaxTimeRemainingOnTurn = g.MaxTimeRemainingUntilTurn,
                    RoundNumber = g.RoundNumber
                }).ToList() ?? []
            };
        });
    }

    /// <summary>
    /// End a Player's Turn
    /// </summary>
    /// <response code="200">End Turn</response>
    /// <response code="500">Returns an error response with an array of errors in the event of an exception or other server error</response>
    [HttpPost("end")]
    [ProducesResponseType(204)]
    [ProducesResponseType(typeof(IApiErrorResult), 500)]
    public async Task<IActionResult> EndPlayerTurnAsync(
        [FromServices] IMediator mediator,
        [FromBody] EndPlayerTurnDto request,
        CancellationToken cancelToken)
    {
        ServiceResultBase response = await mediator.Send(new EndPlayerTurnRequest(request.GameGuid, request.PlayerGuid), cancelToken);

        return response.ToActionResult();
    }
}
