using AscendantEmpire.Api.Utilities;
using AscendantEmpire.Application.Common;
using AscendantEmpire.Application.Features.UnitComponents.Handlers;
using AscendantEmpire.BackendContracts.UnitDesigns.GetUnlockedBodies;
using AscendantEmpire.FrontendContracts;

using MediatR;

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

using static AscendantEmpire.Application.Common.ServiceResultBase;

namespace AscendantEmpire.Api.Controllers;

/// <summary>
/// The player-facing assembly bench: the unlock-scoped component feeds a design is built from, and the custom templates a player saves out of it.
/// </summary>
/// <remarks>
/// <para>
/// Every action here serves the one Unit Designer screen and shares a single authorization posture — a plain <c>[Authorize]</c> with no permission policy — so this controller is reachable by any authenticated player, unlike the dev-tool controllers the rest of the unit-design surface was split into.
/// </para>
/// <para>
/// Identity is never taken from the request. Game and player come from the caller's game token via <c>TryGetGameContext</c>, which answers 403 when no game has been entered; ids carried in a route or body would let a caller read or write another player's designs.
/// </para>
/// </remarks>
[ApiController]
[Route("[controller]")]
public class UnitDesignerController : ControllerBase
{
    /// <summary>
    /// Get the Body components the caller has unlocked, each with its slots and slot rules.
    /// </summary>
    /// <remarks>The game and player are read from the caller's game token, not the request.</remarks>
    /// <response code="200">The unlocked bodies and their slots</response>
    /// <response code="403">No game entered (the token has no game context)</response>
    /// <response code="500">Returns an error response with an array of errors in the event of an exception or other server error</response>
    [Authorize]
    [HttpGet("bodies")]
    [ProducesResponseType(typeof(UnlockedBodiesResult), 200)]
    [ProducesResponseType(typeof(IApiErrorResult), 403)]
    [ProducesResponseType(typeof(IApiErrorResult), 500)]
    public async Task<IActionResult> GetUnlockedBodiesAsync(
        [FromServices] IMediator mediator,
        CancellationToken cancelToken)
    {
        if (!this.TryGetGameContext(out int gameId, out int playerId, out IActionResult? contextError))
        {
            return contextError!;
        }

        ServiceResult<UnlockedBodiesResult> response = await mediator.Send(new GetUnlockedBodiesRequest(gameId, playerId), cancelToken);

        return response.ToActionResult();
    }
}
