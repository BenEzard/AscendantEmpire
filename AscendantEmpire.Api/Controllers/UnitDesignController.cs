using AscendantEmpire.Api.Utilities;
using AscendantEmpire.Application.Common;
using AscendantEmpire.Application.Features.UnitComponents.Handlers;
using AscendantEmpire.BackendContracts.UnitDesigns.GetCompiledUnitTemplate;
using AscendantEmpire.BackendContracts.UnitDesigns.RecompileAllUnitTemplates;
using AscendantEmpire.BackendContracts.UnitDesigns.SaveGlobalUnitTemplate;
using AscendantEmpire.BackendContracts.UnitDesigns.SaveUnitTemplate;
using AscendantEmpire.Domain.Enums.Authentication;
using AscendantEmpire.FrontendContracts;

using MediatR;

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

using static AscendantEmpire.Application.Common.ServiceResultBase;

namespace AscendantEmpire.Api.Controllers;

[ApiController]
[Route("[controller]")]
public class UnitDesignController : ControllerBase
{
    /// <summary>
    /// Update an existing custom (player-owned) unit template, replacing its base and fitted components.
    /// </summary>
    /// <remarks>
    /// The game and player are read from the caller's game token, not the request. Only the template's own
    /// owner may update it; any other caller (or an unknown id) gets a not-found.
    /// </remarks>
    /// <response code="200">The template was updated; returns the updated template</response>
    /// <response code="400">Validation error, or the assembled design fails slot-acceptance rules</response>
    /// <response code="403">No game entered, or a referenced component has not been unlocked by the player</response>
    /// <response code="404">No custom template exists with the given id (owned by the caller), or a referenced component code does not exist</response>
    /// <response code="500">Returns an error response with an array of errors in the event of an exception or other server error</response>
    [Authorize]
    [HttpPut("templates/{templateId:int}")]
    [ProducesResponseType(typeof(UnitTemplateView), 200)]
    [ProducesResponseType(typeof(IApiErrorResult), 400)]
    [ProducesResponseType(typeof(IApiErrorResult), 403)]
    [ProducesResponseType(typeof(IApiErrorResult), 404)]
    [ProducesResponseType(typeof(IApiErrorResult), 500)]
    public async Task<IActionResult> UpdateUnitTemplateAsync(
        [FromServices] IMediator mediator,
        int templateId,
        [FromBody] SaveUnitTemplateDto request,
        CancellationToken cancelToken)
    {
        if (!this.TryGetGameContext(out int gameId, out int playerId, out IActionResult? contextError))
        {
            return contextError!;
        }

        ServiceResult<UnitTemplateView> response =
            await mediator.Send(new UpdateUnitTemplateRequest(gameId, playerId, templateId, request), cancelToken);

        return response.ToActionResult();
    }

    /// <summary>
    /// Get the compiled snapshot of a saved template (aggregated stats, resolved components and capabilities).
    /// </summary>
    /// <remarks>
    /// Not unlock-filtered: the snapshot may contain components the caller never unlocked, so it serves both the player's own designs and enemy designs.
    /// </remarks>
    /// <response code="200">The compiled design snapshot</response>
    /// <response code="404">No compiled snapshot exists for the template</response>
    /// <response code="500">Returns an error response with an array of errors in the event of an exception or other server error</response>
    [Authorize]
    [HttpGet("templates/{templateId:int}/compiled")]
    [ProducesResponseType(typeof(CompiledUnitTemplateView), 200)]
    [ProducesResponseType(typeof(IApiErrorResult), 404)]
    [ProducesResponseType(typeof(IApiErrorResult), 500)]
    public async Task<IActionResult> GetCompiledUnitTemplateAsync(
        [FromServices] IMediator mediator,
        int templateId,
        CancellationToken cancelToken)
    {
        ServiceResult<CompiledUnitTemplateView> response =
            await mediator.Send(new GetCompiledUnitTemplateRequest(templateId), cancelToken);

        return response.ToActionResult();
    }

    /// <summary>
    /// Dev design tool: create a new global/common unit template in the shared catalog.
    /// </summary>
    /// <remarks>
    /// TODO: lock down before any non-dev deployment. This endpoint is intentionally left unauthorized for now;
    /// it must be gated behind a dev/admin policy (or mapped only in Development) before shipping, since it writes the shared template catalog.
    /// Unlike the player-facing create endpoint, this template has no game/player, its Code is required and unique, and no unlock check is applied.
    /// </remarks>
    /// <response code="201">The template was created; returns the created template</response>
    /// <response code="400">Validation error, or the assembled design fails slot-acceptance rules</response>
    /// <response code="404">The base or a fitted component code does not exist in the catalog</response>
    /// <response code="409">A template with the same code already exists</response>
    /// <response code="500">Returns an error response with an array of errors in the event of an exception or other server error</response>
    [Authorize(Policy = nameof(PermissionType.EntitySetupTool))]
    [HttpPost("templates/global")]
    [ProducesResponseType(typeof(GlobalUnitTemplateView), 201)]
    [ProducesResponseType(typeof(IApiErrorResult), 400)]
    [ProducesResponseType(typeof(IApiErrorResult), 404)]
    [ProducesResponseType(typeof(IApiErrorResult), 409)]
    [ProducesResponseType(typeof(IApiErrorResult), 500)]
    public async Task<IActionResult> CreateGlobalUnitTemplateAsync(
        [FromServices] IMediator mediator,
        [FromBody] SaveGlobalUnitTemplateDto request,
        CancellationToken cancelToken)
    {
        ServiceResult<GlobalUnitTemplateView> response = await mediator.Send(new CreateGlobalUnitTemplateRequest(request), cancelToken);

        return response.ToActionResult();
    }

    /// <summary>
    /// Dev design tool: update an existing global/common unit template in the shared catalog.
    /// </summary>
    /// <remarks>
    /// TODO: lock down before any non-dev deployment. This endpoint is intentionally left unauthorized for now;
    /// it must be gated behind a dev/admin policy (or mapped only in Development) before shipping, since it writes the shared template catalog.
    /// </remarks>
    /// <response code="200">The template was updated; returns the updated template</response>
    /// <response code="400">Validation error, or the assembled design fails slot-acceptance rules</response>
    /// <response code="404">No global template exists with the given id, or a referenced component code does not exist</response>
    /// <response code="409">Another template already uses the requested code</response>
    /// <response code="500">Returns an error response with an array of errors in the event of an exception or other server error</response>
    [Authorize(Policy = nameof(PermissionType.EntitySetupTool))]
    [HttpPut("templates/global/{id:int}")]
    [ProducesResponseType(typeof(GlobalUnitTemplateView), 200)]
    [ProducesResponseType(typeof(IApiErrorResult), 400)]
    [ProducesResponseType(typeof(IApiErrorResult), 404)]
    [ProducesResponseType(typeof(IApiErrorResult), 409)]
    [ProducesResponseType(typeof(IApiErrorResult), 500)]
    public async Task<IActionResult> UpdateGlobalUnitTemplateAsync(
        [FromServices] IMediator mediator,
        int id,
        [FromBody] SaveGlobalUnitTemplateDto request,
        CancellationToken cancelToken)
    {
        ServiceResult<GlobalUnitTemplateView> response =
            await mediator.Send(new UpdateGlobalUnitTemplateRequest(id, request), cancelToken);

        return response.ToActionResult();
    }

    /// <summary>
    /// Dev design tool: recompile every unit template's compiled snapshot (global and custom alike).
    /// </summary>
    /// <remarks>
    /// TODO: lock down before any non-dev deployment. This endpoint is intentionally left unauthorized for now;
    /// it must be gated behind a dev/admin policy (or mapped only in Development) before shipping, since it rewrites every compiled snapshot.
    /// Use after editing catalogue components, whose changes don't propagate to the snapshots of templates already built with them.
    /// Best-effort per template: failures are reported in the result without blocking the rest.
    /// </remarks>
    /// <response code="200">The recompilation ran; returns the recompiled count and any per-template failures</response>
    /// <response code="500">Returns an error response with an array of errors in the event of an exception or other server error</response>
    [Authorize(Policy = nameof(PermissionType.EntitySetupTool))]
    [HttpPost("templates/recompile-all")]
    [ProducesResponseType(typeof(RecompileAllUnitTemplatesResult), 200)]
    [ProducesResponseType(typeof(IApiErrorResult), 500)]
    public async Task<IActionResult> RecompileAllUnitTemplatesAsync(
        [FromServices] IMediator mediator,
        CancellationToken cancelToken)
    {
        ServiceResult<RecompileAllUnitTemplatesResult> response =
            await mediator.Send(new RecompileAllUnitTemplatesRequest(), cancelToken);

        return response.ToActionResult();
    }
}
