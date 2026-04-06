using AscendantEmpire.Api.Utilities;
using AscendantEmpire.Application.Common;
using AscendantEmpire.Application.Features.Game.Classes;
using AscendantEmpire.Application.Features.Game.Handlers;
using AscendantEmpire.BackendContracts.Games.AddGame;
using AscendantEmpire.BackendContracts.Games.GetOpenGames;
using AscendantEmpire.FrontendContracts;
using AscendantEmpire.FrontendContracts.Games.GetOpenGames;

using MediatR;

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
    [HttpPost("setup")]
    [ProducesResponseType(201)]
    [ProducesResponseType(typeof(IApiErrorResult), 400)]
    [ProducesResponseType(typeof(IApiErrorResult), 500)]
    public async Task<IActionResult> SetupNewGameAsync(
        [FromServices] IMediator mediator,
        [FromBody] SetupGameDto request)
    {
        ServiceResultBase response = await mediator.Send(new SetupGameRequest(request.MapId, request.GameName, request.NumberOfPlayers));

        return response.ToActionResult();
    }

    /// <summary>
    /// Get a list of Open Games, or a specific one.
    /// </summary>
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
}
