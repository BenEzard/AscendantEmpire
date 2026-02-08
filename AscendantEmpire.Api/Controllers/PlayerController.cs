using AscendantEmpire.Api.Utilities;
using AscendantEmpire.Application.Handlers.Players;
using AscendantEmpire.Application.Handlers.Players.Classes;
using AscendantEmpire.BackendContracts.Players.GetPlayerStructures;
using AscendantEmpire.FrontendContracts;
using AscendantEmpire.FrontendContracts.Player;

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
    [ProducesResponseType(typeof(IPlayerStructuresCollection), 200)]
    [ProducesResponseType(typeof(IApiErrorResult), 500)]
    public async Task<IActionResult> GetPlayerStructuresAsync(
        [FromServices] IMediator mediator,
        [FromQuery] Guid gameGuid,
        [FromQuery] Guid playerGuid,
        CancellationToken cancelToken)
    {
        ServiceResult<IReadOnlyCollection<PlayerStructure>> response = await mediator.Send(new GetPlayerStructuresRequest(gameGuid, playerGuid), cancelToken);

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
}
