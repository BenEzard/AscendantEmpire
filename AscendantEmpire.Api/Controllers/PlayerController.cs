using AscendantEmpire.Api.Utilities;
using AscendantEmpire.Application.Features.Players.Classes;
using AscendantEmpire.Application.Features.Players.Handlers;
using AscendantEmpire.BackendContracts.Players.GetPlayerStructures;
using AscendantEmpire.BackendContracts.Units.GetPlayerTroops;
using AscendantEmpire.FrontendContracts;
using AscendantEmpire.FrontendContracts.Player;
using AscendantEmpire.FrontendContracts.Units;

using MediatR;

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

using static AscendantEmpire.Application.Common.ServiceResultBase;

namespace AscendantEmpire.Api.Controllers;

[ApiController]
[Route("[controller]")]
// Every action here needs a game token: the game and player are read from the token's claims,
// not from the request. A user token (no game entered yet) is rejected by RequireGameContext.
[Authorize]
public class PlayerController : ControllerBase
{
    /// <summary>
    /// Pulls the entered game id and player id out of the caller's game token. Returns null and
    /// an error result when the token has no game context (i.e. the user hasn't entered a game).
    /// </summary>
    private bool TryGetGameContext(out int gameId, out int playerId, out IActionResult? error)
    {
        int? tokenGameId = User.GetGameId();
        int? tokenPlayerId = User.GetPlayerId();

        if (tokenGameId is null || tokenPlayerId is null)
        {
            gameId = 0;
            playerId = 0;
            error = new ObjectResult(new DTApiErrorResult("No game entered. Call /auth/enter-game first."))
            {
                StatusCode = 403
            };
            return false;
        }

        gameId = tokenGameId.Value;
        playerId = tokenPlayerId.Value;
        error = null;
        return true;
    }

    /// <summary>
    /// Get a Player's current Structures
    /// </summary>
    /// <response code="200">Player information for Campaign and Battle Maps</response>
    /// <response code="500">Returns an error response with an array of errors in the event of an exception or other server error</response>
    [HttpGet("structures")]
    [ProducesResponseType(typeof(IPlayerStructureCollection), 200)]
    [ProducesResponseType(typeof(IApiErrorResult), 403)]
    [ProducesResponseType(typeof(IApiErrorResult), 500)]
    public async Task<IActionResult> GetPlayerStructuresAsync(
        [FromServices] IMediator mediator,
        CancellationToken cancelToken)
    {
        // Game and player come from the token, so the caller can't ask for someone else's data.
        if (!TryGetGameContext(out int gameId, out int playerId, out IActionResult? contextError))
        {
            return contextError!;
        }

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
                    OwnerId = s.OwnerId,
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
    [ProducesResponseType(typeof(IApiErrorResult), 403)]
    [ProducesResponseType(typeof(IApiErrorResult), 500)]
    public async Task<IActionResult> GetPlayerTroopsAsync(
        [FromServices] IMediator mediator,
        CancellationToken cancelToken)
    {
        // Game and player come from the token, so the caller can't ask for someone else's data.
        if (!TryGetGameContext(out int gameId, out int playerId, out IActionResult? contextError))
        {
            return contextError!;
        }

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
