using AscendantEmpire.Api.Utilities;
using AscendantEmpire.Application.Handlers.Players;
using AscendantEmpire.Application.Handlers.Players.Classes;
using AscendantEmpire.BackendContracts.Players.GetPlayerStructures;
using AscendantEmpire.BackendContracts.Units.GetPlayerTroops;
using AscendantEmpire.FrontendContracts;
using AscendantEmpire.FrontendContracts.Player;
using AscendantEmpire.FrontendContracts.Units;

using MediatR;

using Microsoft.AspNetCore.Mvc;

using static AscendantEmpire.Application.Common.ServiceResultBase;

namespace AscendantEmpire.Api.Controllers;

[ApiController]
[Route("[controller]")]
public class PlayerController : ControllerBase
{
    /// <summary>
    /// Get a Player's current Structures
    /// </summary>
    /// <response code="200">Player information for Campaign and Battle Maps</response>
    /// <response code="500">Returns an error response with an array of errors in the event of an exception or other server error</response>
    [HttpGet("structures")]
    [ProducesResponseType(typeof(IPlayerStructureCollection), 200)]
    [ProducesResponseType(typeof(IApiErrorResult), 500)]
    public async Task<IActionResult> GetPlayerStructuresAsync(
        [FromServices] IMediator mediator,
        [FromQuery] int gameId,
        [FromQuery] int playerId,
        CancellationToken cancelToken)
    {
        ServiceResult<IReadOnlyCollection<PlayerStructure>> response = await mediator.Send(new GetPlayerStructuresRequest(gameId, playerId), cancelToken);
        return response.ToActionResult(contract =>
        {
            return new DTPlayerStructureCollection
            {
                Structures = response.SuccessValue?.Select(s => new DTPlayerStructure
                {
                    CampaignMapTileId = s.CampaignMapTileId,
                    BattleMapTileId = s.BattleMapTileId,
                    XCoord = s.XCoord,
                    YCoord = s.YCoord,
                    StructureId = s.StructureId,
                    Name = s.Name,
                    Code = s.Code,
                    CompletionPercent = s.CompletionPercent,
                    OwnerPlayerId = s.OwnerPlayerId,
                    SightRange = s.SightRange,
                }).ToList() ?? []
            };
        });
    }

    /// <summary>
    /// Get a Player's current Troops
    /// </summary>
    /// <response code="200">Player information for Campaign and Battle Maps</response>
    /// <response code="500">Returns an error response with an array of errors in the event of an exception or other server error</response>
    [HttpGet("troops")]
    [ProducesResponseType(typeof(IPlayerTroopCollection), 200)]
    [ProducesResponseType(typeof(IApiErrorResult), 500)]
    public async Task<IActionResult> GetPlayerTroopsAsync(
        [FromServices] IMediator mediator,
        [FromQuery] int gameId,
        [FromQuery] int playerId,
        CancellationToken cancelToken)
    {
        ServiceResult<IReadOnlyCollection<PlayerTroop>> response = await mediator.Send(new GetPlayerTroopsRequest(gameId, playerId), cancelToken);

        return response.ToActionResult(contract => response.SuccessValue?.Select(s => new DTPlayerTroop
        {
            CampaignMapTileId = s.CampaignMapTileId,
            BattleMapTileId = s.BattleMapTileId,
            CampaignXCoord = s.CampaignXCoord,
            CampaignYCoord = s.CampaignYCoord,
            SightRange = s.SightRange,
            MapImagePath = s.MapImagePath
        }).ToList() ?? []);
    }
}
