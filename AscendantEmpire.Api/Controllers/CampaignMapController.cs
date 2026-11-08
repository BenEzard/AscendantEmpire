using AscendantEmpire.Api.Utilities;
using AscendantEmpire.Application.Common;
using AscendantEmpire.Application.Common.Messaging;
using AscendantEmpire.Application.Features.Maps.Biomes.Classes;
using AscendantEmpire.Application.Features.Maps.Biomes.Handlers;
using AscendantEmpire.Application.Features.Maps.CampaignMaps.Classes;
using AscendantEmpire.Application.Features.Maps.CampaignMaps.Handlers;
using AscendantEmpire.Application.Features.Maps.Depletion.Classes;
using AscendantEmpire.Application.Features.Maps.Depletion.Handlers;
using AscendantEmpire.Application.Features.Maps.MapResources.Classes;
using AscendantEmpire.Application.Features.Maps.MapResources.Handlers;
using AscendantEmpire.BackendContracts.Biomes;
using AscendantEmpire.BackendContracts.CampaignMaps.GetPlayerCampaignMap;
using AscendantEmpire.BackendContracts.MapResources.GetPlayerMapResources;
using AscendantEmpire.FrontendContracts;
using AscendantEmpire.FrontendContracts.Biomes.GetTileSupportLevels;
using AscendantEmpire.FrontendContracts.Biomes.GetTileSustenance;
using AscendantEmpire.FrontendContracts.CampaignMaps;
using AscendantEmpire.FrontendContracts.MapResources.GetPlayerMapResources;

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

        // The mapping is shared with the Force step endpoint, which answers with the same view of the map from where the Force now stands. Both
        // responses are merged into the same client-side tile cache, so they have to be shaped identically.
        return response.ToActionResult(contract => new DTObservers
        {
            Observers = contract.ToPresentation()
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

    /// <summary>
    /// Get the Forage, Hunt, Fish and Fodder available to the Player on one tile of the Campaign Map.
    /// </summary>
    /// <remarks>
    /// Only disclosed for a tile in the Player's live sight; any other tile answers with <c>isObserved</c> false and no values. A null value means the resource does not exist in
    /// the tile's biome.
    /// </remarks>
    /// <response code="200">The tile's sustenance, as available to the Player</response>
    /// <response code="400">Validation error</response>
    /// <response code="403">The caller has not entered a game</response>
    /// <response code="404">Game not found, or the tile is not on the map</response>
    /// <response code="500">Returns an error response with an array of errors in the event of an exception or other server error</response>
    [HttpGet("player/tiles/{x:int}/{y:int}/sustenance")]
    [ProducesResponseType(typeof(ITileSustenance), 200)]
    [ProducesResponseType(typeof(IApiErrorResult), 400)]
    [ProducesResponseType(typeof(IApiErrorResult), 403)]
    [ProducesResponseType(typeof(IApiErrorResult), 404)]
    [ProducesResponseType(typeof(IApiErrorResult), 500)]
    public async Task<IActionResult> GetTileSustenanceAsync(
        [FromServices] IMediator mediator,
        int x,
        int y,
        CancellationToken cancelToken)
    {
        // Game and player come from the token: a Player's sustenance values are their own, and so is their sight.
        if (!this.TryGetGameContext(out int gameId, out int playerId, out IActionResult? contextError))
        {
            return contextError!;
        }

        ServiceResult<TileSustenance> response = await mediator.Send(new GetTileSustenanceRequest(gameId, playerId, x, y), cancelToken);

        return response.ToActionResult(contract => new DTTileSustenance
        {
            IsObserved = contract.IsObserved,
            Forage = contract.Forage,
            Hunt = contract.Hunt,
            Fish = contract.Fish,
            Fodder = contract.Fodder,
            SupportLevel = contract.SupportLevel
        });
    }

    /// <summary>
    /// Get whether each tile in the Player's live sight can feed the selected Forces, for the hover marker.
    /// </summary>
    /// <remarks>
    /// Asked once per selection, so hovering is a lookup rather than a request per tile. A tile's need is the selected Forces plus the Player's own Forces already on it.
    /// Tiles outside live sight, and tiles with food but nobody to feed, are left out. Turns are forecast with end-of-Round regrowth, for the Forces as they stand.
    /// The selected Forces are repeated in the query (<c>?forceIds=1&amp;forceIds=2</c>), and each must be one of the Player's own Forces.
    /// </remarks>
    /// <response code="200">The support level of each observed tile, for the selected Forces</response>
    /// <response code="400">Validation error, or a Force that is not the Player's</response>
    /// <response code="403">The caller has not entered a game</response>
    /// <response code="404">Game not found</response>
    /// <response code="500">Returns an error response with an array of errors in the event of an exception or other server error</response>
    [HttpGet("player/tiles/support")]
    [ProducesResponseType(typeof(ITileSupportLevels), 200)]
    [ProducesResponseType(typeof(IApiErrorResult), 400)]
    [ProducesResponseType(typeof(IApiErrorResult), 403)]
    [ProducesResponseType(typeof(IApiErrorResult), 404)]
    [ProducesResponseType(typeof(IApiErrorResult), 500)]
    public async Task<IActionResult> GetTileSupportLevelsAsync(
        [FromServices] IMediator mediator,
        [FromQuery] int[] forceIds,
        CancellationToken cancelToken)
    {
        // Game and player come from the token: the levels use the Player's own values, Forces and sight. Only the selection is the caller's to say.
        if (!this.TryGetGameContext(out int gameId, out int playerId, out IActionResult? contextError))
        {
            return contextError!;
        }

        ServiceResult<TileSupportLevels> response = await mediator.Send(new GetTileSupportLevelsRequest(gameId, playerId, forceIds), cancelToken);

        return response.ToActionResult(contract => new DTTileSupportLevels
        {
            Tiles = [.. contract.Tiles.Select(tile => new DTTileSupportLevelEntry
            {
                X = tile.X,
                Y = tile.Y,
                SupportLevel = tile.SupportLevel,
                Turns = tile.Turns
            })]
        });
    }

    /// <summary>
    /// Get the MapResources the Player has discovered on the Campaign Map.
    /// </summary>
    /// <remarks>
    /// Fetched once for the whole map so drawing a tile is a lookup. An instance the Player has not discovered is never included; one they have stays included
    /// wherever they are, but carries an <c>estimatedQuantity</c> only while the tile is observed, which <c>isObserved</c> reports. Quantities are this
    /// Player's own estimate, never the true amount on the tile.
    /// </remarks>
    /// <response code="200">The MapResources visible to the Player</response>
    /// <response code="400">Validation error</response>
    /// <response code="403">The caller has not entered a game</response>
    /// <response code="404">Game not found</response>
    /// <response code="500">Returns an error response with an array of errors in the event of an exception or other server error</response>
    [HttpGet("player/map-resources")]
    [ProducesResponseType(typeof(IPlayerMapResources), 200)]
    [ProducesResponseType(typeof(IApiErrorResult), 400)]
    [ProducesResponseType(typeof(IApiErrorResult), 403)]
    [ProducesResponseType(typeof(IApiErrorResult), 404)]
    [ProducesResponseType(typeof(IApiErrorResult), 500)]
    public async Task<IActionResult> GetPlayerMapResourcesAsync([FromServices] IMediator mediator, CancellationToken cancelToken)
    {
        // Game and player come from the token: what is visible is decided by this Player's own discoveries and their own sight.
        if (!this.TryGetGameContext(out int gameId, out int playerId, out IActionResult? contextError))
        {
            return contextError!;
        }

        ServiceResult<PlayerMapResources> response = await mediator.Send(new GetPlayerMapResourcesRequest(gameId, playerId), cancelToken);

        return response.ToActionResult(contract => new DTPlayerMapResources
        {
            Resources = [.. contract.Resources.Select(resource => new DTPlayerMapResourceEntry
            {
                X = resource.X,
                Y = resource.Y,
                Slot = resource.Slot,
                MapResourceTypeId = resource.MapResourceTypeId,
                Code = resource.Code,
                DisplayName = resource.DisplayName,
                IconPath = resource.IconPath,
                IsObserved = resource.IsObserved,
                EstimatedQuantity = resource.EstimatedQuantity
            })]
        });
    }
}
