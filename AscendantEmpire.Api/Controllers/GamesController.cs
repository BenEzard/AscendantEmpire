using AscendantEmpire.Api.Utilities;
using AscendantEmpire.Application.Common;
using AscendantEmpire.Application.Common.Messaging;
using AscendantEmpire.Application.Features.Game.Classes;
using AscendantEmpire.Application.Features.Game.Handlers;
using AscendantEmpire.BackendContracts.Games.AddGame;
using AscendantEmpire.BackendContracts.Games.GetGames;
using AscendantEmpire.BackendContracts.Games.GetMyGames;
using AscendantEmpire.BackendContracts.Games.GetNewGameSettings;
using AscendantEmpire.Domain.Enums.Authentication;
using AscendantEmpire.FrontendContracts;
using AscendantEmpire.FrontendContracts.Games.GetGames;
using AscendantEmpire.FrontendContracts.Games.GetMyGames;
using AscendantEmpire.FrontendContracts.Games.Settings;

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
    /// List the in-progress Games the calling account holds an active Player slot in, for the Open Game dialog.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The account comes from the bearer token and the request carries nothing, so there is no filter to impersonate anyone with. Two accounts calling this
    /// get two different lists, and neither can see the other's games.
    /// </para>
    /// <para>
    /// One row per Game, not per Player. Alongside the caller's own slot each row names whoever the campaign queue is currently waiting on, which is what
    /// the dialog shows in place of the old lobby's list of every ruler in every game.
    /// </para>
    /// </remarks>
    /// <response code="200">The caller's games</response>
    /// <response code="401">Missing or invalid token</response>
    /// <response code="500">Returns an error response with an array of errors in the event of an exception or other server error</response>
    [Authorize]
    [HttpGet("mine")]
    [ProducesResponseType(typeof(IMyGameCollection), 200)]
    [ProducesResponseType(401)]
    [ProducesResponseType(typeof(IApiErrorResult), 500)]
    public async Task<IActionResult> GetMyGamesAsync(
        [FromServices] IMediator mediator,
        CancellationToken cancelToken)
    {
        // The user id comes from the proven token, never the request.
        int? userId = User.GetUserId();
        if (userId is null)
        {
            return Unauthorized();
        }

        ServiceResult<IReadOnlyCollection<MyGame>> response = await mediator.Send(new GetMyGamesRequest(userId.Value), cancelToken);

        return response.ToActionResult(contract => new DTMyGameCollection
        {
            Games = response.SuccessValue?.Select(IMyGame (g) => new DTMyGame
            {
                GameId = g.GameId,
                GameName = g.GameName,
                MapWidth = g.MapWidth,
                MapHeight = g.MapHeight,
                YourPlayerId = g.YourPlayerId,
                YourRulerLabel = g.YourRulerLabel,
                YourCampaignOffsetId = g.YourCampaignOffsetId,
                CurrentTurnPlayerId = g.CurrentTurnPlayerId,
                CurrentTurnRulerLabel = g.CurrentTurnRulerLabel,
                IsYourTurn = g.IsYourTurn,
            }).ToList() ?? []
        });
    }

    /// <summary>
    /// Get the settings which should be used when creating a new game
    /// </summary>
    /// <remarks>
    /// </remarks>
    /// <response code="200">The settings that must be used to create a new game.</response>
    /// <response code="401">Missing or invalid token</response>
    /// <response code="500">Returns an error response with an array of errors in the event of an exception or other server error</response>
    [Authorize]
    [HttpGet("settings")]
    [ProducesResponseType(typeof(INewGameSettings), 200)]
    [ProducesResponseType(401)]
    [ProducesResponseType(typeof(IApiErrorResult), 500)]
    public async Task<IActionResult> GetGameSettingsAsync([FromServices] IMediator mediator, CancellationToken cancelToken)
    {
        // The user id comes from the proven token, never the request.
        int? userId = User.GetUserId();
        if (userId is null)
        {
            return Unauthorized();
        }

        ServiceResult<NewGameSettings> response = await mediator.Send(new GetNewGameSettingsRequest(), cancelToken);

        return response.ToActionResult(contract => new DTNewGameSettings
        {
            MinimumPlayersPerGame = response.SuccessValue?.MinimumPlayersPerGame ?? 0,
            MaximumPlayersPerGame = response.SuccessValue?.MaximumPlayersPerGame ?? 0,
            BattleMapSettings = new DTBattleMapSettings
            {
                MinimumBattleMapSize = response.SuccessValue?.MinimumBattleMapSize ?? 0,
                MaximumBattleMapSize = response.SuccessValue?.MaximumBattleMapSize ?? 0,
                DefaultBattleMapSize = response.SuccessValue?.DefaultBattleMapSize ?? 0,
                BattleMapSizeIncrement = response.SuccessValue?.BattleMapSizeIncrement ?? 0
            },
            TechnologySettings = new DTTechnologySettings
            {
                BaseResearchPoints = response.SuccessValue?.BaseResearchPoints ?? 0,
                TechnologySpeedMultiplierPercent = response.SuccessValue?.TechnologySpeedMultiplierPercent ?? 0,
                TechnologyFuzzinessPercent = response.SuccessValue?.TechnologyFuzzinessPercent ?? 0
            }
        });
    }

    /// <summary>
    /// Dev tool: list every Game, whatever state it is in, for the System panel's Games page.
    /// </summary>
    /// <remarks>
    /// Deliberately unfiltered, unlike the matchmaking and my-games listings — a Game that failed setup or has been abandoned is exactly the kind an
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
