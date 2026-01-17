using AscendantEmpire.Api.Utilities;
using AscendantEmpire.Application.Handlers.Maps.CampaignMaps;
using AscendantEmpire.Application.Handlers.Maps.CampaignMaps.Classes;
using AscendantEmpire.BackendContracts.CampaignMap;
using AscendantEmpire.FrontendContracts;
using AscendantEmpire.FrontendContracts.CampaignMaps;

using MediatR;

using Microsoft.AspNetCore.Mvc;

using static AscendantEmpire.Application.Common.ServiceResultBase;

namespace AscendantEmpire.Api.Controllers;

[ApiController]
[Route("[controller]")]
public class CampaignMapController : ControllerBase
{
    /// <summary>
    /// Get the Player's view of the Campaign Map for a given Game.
    /// </summary>
    /// <response code="200">Requested data returned for the Player</response>
    /// <response code="400">Validation error</response>
    /// <response code="404">Game or Player not found</response>
    /// <response code="500">Returns an error response with an array of errors in the event of an exception or other server error</response>
    [HttpPost("player")]
    [ProducesResponseType(typeof(IObservedCampaignTileCollection), 200)]
    [ProducesResponseType(typeof(IApiErrorResult), 400)]
    [ProducesResponseType(typeof(IApiErrorResult), 404)]
    [ProducesResponseType(typeof(IApiErrorResult), 500)]
    public async Task<IActionResult> GetPlayerCampaignMapTilesAsync(
        [FromServices] IMediator mediator,
        [FromBody] GetPlayerCampaignMapDto request)
    {
        ServiceResult<IReadOnlyCollection<ObservedCampaignTile>> response = await mediator.Send(new GetPlayerCampaignMapRequest(
            request.GameGuid, 
            request.PlayerGuid, 
            request.Observers,
            request.LoadWholeMap));

        return response.ToActionResult(contract =>
        {
            return new DTObservedCampaignTileCollection
            {
                Tiles = contract.Select(tile => new DTObservedCampaignTile
                {
                    XCoord = tile.XCoord,
                    YCoord = tile.YCoord,
                    BiomeTypeId = tile.BiomeTypeId,
                    CampaignMapGeographyTypeId = tile.CampaignMapGeographyTypeId
                }).ToList()
            };
        });
    }
}
