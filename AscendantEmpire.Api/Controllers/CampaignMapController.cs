using AscendantEmpire.Api.Utilities;
using AscendantEmpire.Application.Handlers.Maps.CampaignMaps;
using AscendantEmpire.Application.Handlers.Maps.CampaignMaps.Classes;
using AscendantEmpire.BackendContracts.CampaignMaps.GetPlayerCampaignMap;
using AscendantEmpire.BackendContracts.CampaignMaps.HidePlayerCampaignMap;
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
    [ProducesResponseType(typeof(IMapObservers), 200)]
    [ProducesResponseType(typeof(IApiErrorResult), 400)]
    [ProducesResponseType(typeof(IApiErrorResult), 404)]
    [ProducesResponseType(typeof(IApiErrorResult), 500)]
    public async Task<IActionResult> GetPlayerCampaignMapTilesAsync(
        [FromServices] IMediator mediator,
        [FromBody] GetPlayerCampaignMapDto request)
    {
        ServiceResult<IReadOnlyCollection<ObserverSight>> response = await mediator.Send(new GetPlayerCampaignMapRequest(
            request.GameGuid, 
            request.PlayerGuid, 
            request.Observers,
            request.LoadWholeMap));

        return response.ToActionResult(contract =>
        {
            return new DTObservers
            {
                Observers = contract.Select(o => new DTObserver
                {
                    Id = o.Id,

                    XCoord = o.Location.X,
                    YCoord = o.Location.Y,
                    SeenTiles = o.SeenTiles.Select(tile => new DTObservedCampaignTile
                    {
                        XCoord = tile.XCoord,
                        YCoord = tile.YCoord,
                        BiomeTypeId = tile.BiomeTypeId,
                        CampaignMapGeographyTypeId = tile.CampaignMapGeographyTypeId,
                        Structures = [.. tile.Structures?.Select(s => new DTStructureStub
                        {
                            Id = s.Id,
                            RecordType = s.RecordType,
                            StructureId = s.StructureId,
                            PositionOnTile = s.PositionOnTile,
                            CompletionPercent = s.CompletionPercent
                        }) ?? []],
                        Troops = [.. tile.Troops?.Select(t => new DTTroopStub
                        {
                            Id = t.Id,
                            RecordType = t.RecordType,
                            UnitDesignId = t.UnitDesignId,
                            HealthyTroops = t.HealthyTroops,
                            InjuredTroops = t.InjuredTroops,
                            InitialStrength = t.InitialStrength
                        }) ?? []]
                    }).ToList()
                }).ToList()
            };
        });
    }

    /// <summary>
    /// [DEVELOPER TOOL] Hide the Player's view of the Campaign Map for a given Game.
    /// </summary>
    /// <response code="200">Requested data returned for the Player</response>
    /// <response code="400">Validation error</response>
    /// <response code="404">Game or Player not found</response>
    /// <response code="500">Returns an error response with an array of errors in the event of an exception or other server error</response>
    [HttpPost("player/clear")]
    [ProducesResponseType(200)]
    [ProducesResponseType(typeof(IApiErrorResult), 400)]
    [ProducesResponseType(typeof(IApiErrorResult), 404)]
    [ProducesResponseType(typeof(IApiErrorResult), 500)]
    public async Task HidePlayerCampaignMapAsync(
        [FromServices] IMediator mediator,
        [FromBody] HidePlayerCampaignMapDto request)
    {
        await mediator.Send(new HidePlayerCampaignMapRequest(request.GameGuid, request.PlayerGuid));
    }
}
