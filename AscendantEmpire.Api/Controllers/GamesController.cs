using AscendantEmpire.Api.Utilities;
using AscendantEmpire.Application.Common;
using AscendantEmpire.Application.Features.Game.Classes;
using AscendantEmpire.Application.Features.Game.Handlers;
using AscendantEmpire.BackendContracts.Games.AddGame;
using AscendantEmpire.BackendContracts.Games.GetGames;
using AscendantEmpire.BackendContracts.Games.GetOpenGames;
using AscendantEmpire.Domain.Enums.Authentication;
using AscendantEmpire.FrontendContracts;
using AscendantEmpire.FrontendContracts.Games.GetGames;
using AscendantEmpire.FrontendContracts.Games.GetOpenGames;

using MediatR;

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

using static AscendantEmpire.Application.Common.ServiceResultBase;

namespace AscendantEmpire.Api.Controllers;

[ApiController]
[Route("[controller]")]
public class GamesController : ControllerBase
{
    /// <summary>
    /// Add Game
    /// </summary>
    /// <response code="201">Game successfully created</response>
    /// <response code="400">Validation error</response>
    /// <response code="500">Returns an error response with an array of errors in the event of an exception or other server error</response>
    [Authorize(Policy = nameof(PermissionType.CreateGame))]
    [HttpPost("setup")]
    [ProducesResponseType(201)]
    [ProducesResponseType(typeof(IApiErrorResult), 400)]
    [ProducesResponseType(typeof(IApiErrorResult), 500)]
    public async Task<IActionResult> SetupNewGameAsync(
        [FromServices] IMediator mediator,
        [FromBody] SetupGameDto request)
    {
        ServiceResultBase response = await mediator.Send(new SetupGameRequest(request.MapId, request.GameName, request.NumberOfPlayers, request.BattleMapSize, request.GenerationSeed));

        return response.ToActionResult();
    }

    /// <summary>
    /// Get a list of Open Games, or a specific one.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The <c>GameId</c> and <c>PlayerId</c> in the body are optional <i>filters</i>, not an identity claim, and they are not a pattern to copy. This is a
    /// lobby listing reached <b>before</b> the caller has entered a game, so there is no game token to read them from, and it returns only what a lobby
    /// shows anyway: game name, map size and the ruler labels of the active slots.
    /// </para>
    /// <para>
    /// Every game-scoped action elsewhere reads its identity from the token via <c>TryGetGameContext</c>. See <c>EndpointIdentityBindingTests</c>, which
    /// enforces that and names this endpoint as one of a handful of justified exceptions.
    /// </para>
    /// </remarks>
    /// <response code="200">List of Open Games</response>
    /// <response code="400">Validation error</response>
    /// <response code="404">Game or Player not found</response>
    /// <response code="500">Returns an error response with an array of errors in the event of an exception or other server error</response>
    [HttpPost("open")]
    [ProducesResponseType(typeof(IOpenGameCollection), 200)]
    [ProducesResponseType(typeof(IApiErrorResult), 400)]
    [ProducesResponseType(typeof(IApiErrorResult), 404)]
    [ProducesResponseType(typeof(IApiErrorResult), 500)]
    public async Task<IActionResult> LoadGameAsync(
        [FromServices] IMediator mediator,
        [FromBody] GetOpenGamesDto request)
    {
        ServiceResult<IReadOnlyCollection<OpenGame>> response = await mediator.Send(new GetOpenGamesRequest(request.GameId, request.PlayerId));

        return response.ToActionResult(contract =>
        {
            return new DTOpenGameCollection
            {
                Games = response.SuccessValue?.Select(g => new DTOpenGame
                {
                    GameId = g.GameId,
                    GameName = g.GameName,
                    MapWidth = g.MapWidth,
                    MapHeight = g.MapHeight,
                    Players = g.Players.Select(p => new DTOpenGamePlayer
                    {
                        PlayerId = p.PlayerId,
                        RulerTitle = p.RulerTitle,
                        RulerName = p.RulerName,
                        FactionName = p.FactionName,
                        RulerLabel = p.RulerLabel,
                        CampaignOffsetId = p.CampaignMapOffsetId,
                    }).ToList() ?? []
                }).ToList() ?? []
            };
        });
    }

    /// <summary>
    /// Dev tool: list every Game, whatever state it is in, for the System panel's Games page.
    /// </summary>
    /// <remarks>
    /// Deliberately unfiltered, unlike the matchmaking and open-game listings — a Game that failed setup or has been abandoned is exactly the kind an
    /// administrator has come here to find. It is gated on <c>DeleteGame</c> rather than on <c>SystemPanel</c> because deleting is the only thing the page
    /// it feeds does.
    /// </remarks>
    /// <response code="200">The list of games.</response>
    /// <response code="401">Missing or invalid token</response>
    /// <response code="403">The account does not hold the DeleteGame permission</response>
    /// <response code="500">Returns an error response with an array of errors in the event of an exception or other server error</response>
    [Authorize(Policy = nameof(PermissionType.DeleteGame))]
    [HttpGet]
    [ProducesResponseType(typeof(IGameSummaryCollection), 200)]
    [ProducesResponseType(401)]
    [ProducesResponseType(403)]
    [ProducesResponseType(typeof(IApiErrorResult), 500)]
    public async Task<IActionResult> GetAllGamesAsync(
        [FromServices] IMediator mediator,
        CancellationToken cancelToken)
    {
        ServiceResult<IReadOnlyCollection<GameSummary>> response = await mediator.Send(new GetAllGamesRequest(), cancelToken);

        return response.ToActionResult(contract => new DTGameSummaryCollection
        {
            Games = response.SuccessValue?.Select(IGameSummary (g) => new DTGameSummary
            {
                GameId = g.GameId,
                GameName = g.GameName,
                NumberOfPlayers = g.NumberOfPlayers,
                ClaimedPlayerCount = g.ClaimedPlayerCount,
                CreationStatus = g.CreationStatus,
                PlayStatus = g.PlayStatus,
                CreatedAt = g.CreatedAt,
            }).ToList() ?? []
        });
    }

    /// <summary>
    /// Dev tool: permanently delete a Game and every row scoped to it. The purge runs asynchronously — poll <c>GET /system/status?processId=X</c> for progress.
    /// </summary>
    /// <remarks>
    /// <para>
    /// There is no undo. Players, campaign and battle maps, units, orders, structures, battles, turn queues and per-player design and military state all go;
    /// catalogue data, user accounts and the global unit templates are untouched.
    /// </para>
    /// <para>
    /// The Game's status does not restrict this. One that is mid-setup, mid-play or abandoned is the likeliest thing to need removing, and refusing any of
    /// those would leave the wreckage no way out.
    /// </para>
    /// </remarks>
    /// <response code="201">Delete job accepted. Returns the processId.</response>
    /// <response code="400">Validation error</response>
    /// <response code="401">Missing or invalid token</response>
    /// <response code="403">The account does not hold the DeleteGame permission</response>
    /// <response code="404">The game does not exist</response>
    /// <response code="409">A delete of this game is already queued or running</response>
    /// <response code="500">Returns an error response with an array of errors in the event of an exception or other server error</response>
    [Authorize(Policy = nameof(PermissionType.DeleteGame))]
    [HttpDelete("{gameId:int}")]
    [ProducesResponseType(typeof(int), 201)]
    [ProducesResponseType(typeof(IApiErrorResult), 400)]
    [ProducesResponseType(401)]
    [ProducesResponseType(403)]
    [ProducesResponseType(typeof(IApiErrorResult), 404)]
    [ProducesResponseType(typeof(IApiErrorResult), 409)]
    [ProducesResponseType(typeof(IApiErrorResult), 500)]
    public async Task<IActionResult> DeleteGameAsync(
        [FromServices] IMediator mediator,
        [FromRoute] int gameId,
        CancellationToken cancelToken)
    {
        ServiceResult<int> response = await mediator.Send(new InitiateGameDeleteRequest(gameId), cancelToken);

        return response.ToActionResult();
    }
}
