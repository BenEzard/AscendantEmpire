using AscendantEmpire.Api.Utilities;
using AscendantEmpire.Application.Common;
using AscendantEmpire.Application.Common.Messaging;
using AscendantEmpire.Application.Features.Units.Classes;
using AscendantEmpire.Application.Features.Units.Handlers;
using AscendantEmpire.BackendContracts.Units.GetUnitDisplayInfo;
using AscendantEmpire.BackendContracts.Units.Spawn;
using AscendantEmpire.Domain.Enums.Authentication;
using AscendantEmpire.FrontendContracts;
using AscendantEmpire.FrontendContracts.Units.GetUnitDisplayInfo;

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

using static AscendantEmpire.Application.Common.ServiceResultBase;

namespace AscendantEmpire.Api.Controllers;

[ApiController]
[Route("[controller]")]
public class UnitsController : ControllerBase
{
    /// <summary>
    /// Spawn a Unit into a game and location.
    /// </summary>
    /// <remarks>
    /// <para>
    /// This action takes the game and player from the request body on purpose, and is <b>not</b> a pattern to copy. It is a developer tool gated behind the
    /// <see cref="PermissionType.EntitySetupTool"/> permission, and placing a unit into an arbitrary game for an arbitrary player is the whole point of it —
    /// the caller is deliberately acting outside any game they have entered, so there is no token context to read.
    /// </para>
    /// <para>
    /// Every game-scoped action elsewhere reads its identity from the token via <c>TryGetGameContext</c>. See <c>EndpointIdentityBindingTests</c>, which
    /// enforces that and names this endpoint as one of a handful of justified exceptions.
    /// </para>
    /// </remarks>
    /// <response code="201">End Turn</response>
    /// <response code="500">Returns an error response with an array of errors in the event of an exception or other server error</response>
    [Authorize(Policy = nameof(PermissionType.EntitySetupTool))]
    [HttpPost("temporary-dev-only/spawn")]
    [ProducesResponseType(201)]
    [ProducesResponseType(typeof(IApiErrorResult), 500)]
    public async Task<IActionResult> SpawnUnitAsync(
        [FromServices] IMediator mediator,
        [FromBody] SpawnUnitDto request,
        CancellationToken cancelToken)
    {
        ServiceResultBase response = await mediator.Send(new SpawnUnitRequest(request.GameId,
            request.PlayerId,
            request.XCoord,
            request.YCoord,
            request.UnitTemplateId,
            request.InitialStrength,
            request.HealthyCount,
            request.InjuredCount),
            cancelToken);

        return response.ToActionResult();
    }

    /// <summary>
    /// Get the unit template display information which maps the UnitTemplateId to the image which should be shown on the map.
    /// </summary>
    /// <response code="201">End Turn</response>
    /// <response code="403">The caller has not entered a game</response>
    /// <response code="500">Returns an error response with an array of errors in the event of an exception or other server error</response>
    [Authorize]
    [HttpGet("display")]
    [ProducesResponseType(typeof(IUnitDisplayInfoCollection), 200)]
    [ProducesResponseType(typeof(IApiErrorResult), 403)]
    [ProducesResponseType(typeof(IApiErrorResult), 500)]
    public async Task<IActionResult> GetUnitsAsync(
        [FromServices] IMediator mediator,
        CancellationToken cancelToken)
    {
        // The game comes from the token, so the caller can't read another game's unit display info.
        if (!this.TryGetGameContext(out int gameId, out int _, out IActionResult? contextError))
        {
            return contextError!;
        }

        ServiceResult<IReadOnlyCollection<UnitDisplayInfo>> response = await mediator.Send(new GetUnitDisplayInfoRequest(gameId), cancelToken);

        return response.ToActionResult(contract =>
        {
            return new DTUnitDisplayInfoCollection
            {
                UnitDisplayInfo = contract.Select(t => new DTUnitDisplayInfo
                {
                    UnitTemplateId = t.UnitTemplateId,
                    MapImagePath = t.MapImagePath,
                    DisplayName = t.DisplayName
                }).ToList() ?? []
            };
        });
    }
}
