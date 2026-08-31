using AscendantEmpire.Api.Utilities;
using AscendantEmpire.Application.Common;
using AscendantEmpire.Application.Features.Maps.CampaignMaps.Classes;
using AscendantEmpire.Application.Features.Maps.CampaignMaps.Handlers;
using AscendantEmpire.BackendContracts.CampaignMaps.GetPlayerCampaignMap;
using AscendantEmpire.FrontendContracts;
using AscendantEmpire.FrontendContracts.CampaignMaps;

using MediatR;

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

using static AscendantEmpire.Application.Common.ServiceResultBase;

namespace AscendantEmpire.Api.Controllers;

[ApiController]
[Route("[controller]")]
// Every action here needs a game token: the game and player are read from the token's claims,
// not from the request. A user token (no game entered yet) is rejected by TryGetGameContext.
[Authorize]
public class CampaignMapController : ControllerBase
{
    /// <summary>
    /// Get the Player's view of the Campaign Map for the Game they have entered.
    /// </summary>
    /// <response code="200">Requested data returned for the Player</response>
    /// <response code="400">Validation error</response>
    /// <response code="403">The caller has not entered a game</response>
    /// <response code="404">Game or Player not found</response>
    /// <response code="500">Returns an error response with an array of errors in the event of an exception or other server error</response>
    [HttpPost("player")]
    [ProducesResponseType(typeof(IMapObservers), 200)]
    [ProducesResponseType(typeof(IApiErrorResult), 400)]
    [ProducesResponseType(typeof(IApiErrorResult), 403)]
    [ProducesResponseType(typeof(IApiErrorResult), 404)]
    [ProducesResponseType(typeof(IApiErrorResult), 500)]
    public async Task<IActionResult> GetPlayerCampaignMapTilesAsync(
        [FromServices] IMediator mediator,
        [FromBody] GetPlayerCampaignMapDto request)
    {
        // Game and player come from the token, so the caller can't read someone else's fog of war. The body carries only how much of the map to load.
        if (!this.TryGetGameContext(out int gameId, out int playerId, out IActionResult? contextError))
        {
            return contextError!;
        }

        ServiceResult<IReadOnlyCollection<ObserverSight>> response = await mediator.Send(new GetPlayerCampaignMapRequest(
            gameId,
            playerId,
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
                            OwnerPlayerId = s.OwnerPlayerId,
                            StructureId = s.StructureId,
                            PositionOnTile = s.PositionOnTile,
                            CompletionPercent = s.CompletionPercent
                        }) ?? []],
                        Troops = [.. tile.Troops?.Select(t => new DTTroopStub
                        {
                            Id = t.Id,
                            RecordType = t.RecordType,
                            OwnerPlayerId = t.OwnerPlayerId,
                            UnitTemplateId = t.UnitTemplateId,
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
    /// [DEVELOPER TOOL] Hide the Player's view of the Campaign Map for the Game they have entered.
    /// </summary>
    /// <remarks>
    /// Returns the handler's result rather than swallowing it — the action has to be able to answer with the 403 from <c>TryGetGameContext</c>, and a failed
    /// clear used to come back as a bare 200.
    /// </remarks>
    /// <response code="200">Requested data returned for the Player</response>
    /// <response code="400">Validation error</response>
    /// <response code="403">The caller has not entered a game</response>
    /// <response code="404">Game or Player not found</response>
    /// <response code="500">Returns an error response with an array of errors in the event of an exception or other server error</response>
    [HttpPost("player/clear")]
    [ProducesResponseType(200)]
    [ProducesResponseType(typeof(IApiErrorResult), 400)]
    [ProducesResponseType(typeof(IApiErrorResult), 403)]
    [ProducesResponseType(typeof(IApiErrorResult), 404)]
    [ProducesResponseType(typeof(IApiErrorResult), 500)]
    public async Task<IActionResult> HidePlayerCampaignMapAsync([FromServices] IMediator mediator)
    {
        // Game and player come from the token, so the caller can't clear someone else's fog of war. The action takes no body at all.
        if (!this.TryGetGameContext(out int gameId, out int playerId, out IActionResult? contextError))
        {
            return contextError!;
        }

        ServiceResultBase response = await mediator.Send(new HidePlayerCampaignMapRequest(gameId, playerId));

        return response.ToActionResult();
    }
}
