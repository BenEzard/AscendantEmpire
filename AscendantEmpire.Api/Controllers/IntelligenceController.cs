using AscendantEmpire.Api.Utilities;
using AscendantEmpire.Application.Common.Messaging;
using AscendantEmpire.Application.Features.Intelligence.Classes;
using AscendantEmpire.Application.Features.Intelligence.Handlers;
using AscendantEmpire.BackendContracts.Intelligence.GetPlayerKnownForces;
using AscendantEmpire.BackendContracts.Intelligence.GetPlayerSightingReports;
using AscendantEmpire.FrontendContracts;
using AscendantEmpire.FrontendContracts.Intelligence;

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

using static AscendantEmpire.Application.Common.ServiceResultBase;

namespace AscendantEmpire.Api.Controllers;

[ApiController]
[Route("[controller]")]
// Every action here needs a game token: the game and player are read from the token's claims, not from the request, so a caller can never read another Player's intelligence. A user token (no
// game entered yet) is rejected by TryGetGameContext.
[Authorize]
public class IntelligenceController : ControllerBase
{
    /// <summary>
    /// Get the calling Player's intelligence feed: every report raised for them since their last Turn ended.
    /// </summary>
    /// <remarks>
    /// Each report is already stripped down to the fidelity its sighting earned. Reports carry no text; the client composes the sentence.
    /// </remarks>
    /// <response code="200">The Player's reports, oldest first</response>
    /// <response code="403">The caller has not entered a game</response>
    /// <response code="404">Game or Player not found</response>
    /// <response code="500">Returns an error response with an array of errors in the event of an exception or other server error</response>
    [HttpGet("reports")]
    [ProducesResponseType(typeof(ISightingReportFeed), 200)]
    [ProducesResponseType(typeof(IApiErrorResult), 403)]
    [ProducesResponseType(typeof(IApiErrorResult), 404)]
    [ProducesResponseType(typeof(IApiErrorResult), 500)]
    public async Task<IActionResult> GetSightingReportsAsync(
        [FromServices] IMediator mediator,
        CancellationToken cancelToken)
    {
        if (!this.TryGetGameContext(out int gameId, out int playerId, out IActionResult? contextError))
        {
            return contextError!;
        }

        ServiceResult<IReadOnlyCollection<SightingReport>> response = await mediator.Send(new GetPlayerSightingReportsRequest(gameId, playerId), cancelToken);

        return response.ToActionResult(contract => new DTSightingReportFeed
        {
            Reports = [.. contract ?? []]
        });
    }

    /// <summary>
    /// Get every Force the calling Player has watched, as they last saw it.
    /// </summary>
    /// <remarks>
    /// Each is already stripped down to the fidelity its last sighting earned. Forces the Player can currently see are included; the client decides which markers to hide.
    /// </remarks>
    /// <response code="200">The Player's known Forces, oldest sighting first</response>
    /// <response code="403">The caller has not entered a game</response>
    /// <response code="404">Game or Player not found</response>
    /// <response code="500">Returns an error response with an array of errors in the event of an exception or other server error</response>
    [HttpGet("known-forces")]
    [ProducesResponseType(typeof(IKnownForceCollection), 200)]
    [ProducesResponseType(typeof(IApiErrorResult), 403)]
    [ProducesResponseType(typeof(IApiErrorResult), 404)]
    [ProducesResponseType(typeof(IApiErrorResult), 500)]
    public async Task<IActionResult> GetKnownForcesAsync(
        [FromServices] IMediator mediator,
        CancellationToken cancelToken)
    {
        if (!this.TryGetGameContext(out int gameId, out int playerId, out IActionResult? contextError))
        {
            return contextError!;
        }

        ServiceResult<IReadOnlyCollection<KnownForce>> response = await mediator.Send(new GetPlayerKnownForcesRequest(gameId, playerId), cancelToken);

        return response.ToActionResult(contract => new DTKnownForceCollection
        {
            KnownForces = [.. contract ?? []]
        });
    }
}
