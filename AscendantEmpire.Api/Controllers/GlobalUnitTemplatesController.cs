using AscendantEmpire.Api.Utilities;
using AscendantEmpire.Application.Common;
using AscendantEmpire.Application.Features.UnitComponents.Handlers;
using AscendantEmpire.BackendContracts.UnitDesigns.RecompileAllUnitTemplates;
using AscendantEmpire.BackendContracts.UnitDesigns.SaveGlobalUnitTemplate;
using AscendantEmpire.BackendContracts.UnitDesigns.Shared;
using AscendantEmpire.Domain.Enums.Authentication;
using AscendantEmpire.FrontendContracts;

using MediatR;

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

using static AscendantEmpire.Application.Common.ServiceResultBase;

namespace AscendantEmpire.Api.Controllers;

/// <summary>
/// The dev tool's authoring surface for global/common unit templates — the shared, gameless template catalogue every player draws from.
/// </summary>
/// <remarks>
/// <para>
/// Global templates and the player templates on <c>UnitDesignerController</c> share a table but are different resources: a global template has no game and no player, its <c>Code</c> is required and unique, and no unlock check is applied when it is saved. That is why they are authored through a separate controller rather than a <c>global</c> sub-route of the player one.
/// </para>
/// <para>
/// Every action is gated behind <see cref="PermissionType.EntitySetupTool"/>, since all three write the shared catalogue rather than anything the caller owns. The recompile-all maintenance action is the one whose reach is wider than this controller's name: it rewrites custom snapshots too, and lives here for its permission gate rather than its scope.
/// </para>
/// </remarks>
[ApiController]
[Route("[controller]")]
public class GlobalUnitTemplatesController : ControllerBase
{
    /// <summary>
    /// Dev design tool: save a global/common unit template into the shared catalogue, creating it or — on confirmation — overwriting the one that already
    /// holds its Code.
    /// </summary>
    /// <remarks>
    /// <para>Gated behind <see cref="PermissionType.EntitySetupTool"/>, since it writes the shared template catalogue.
    /// Unlike the player-facing save endpoint, this template has no game/player, its Code is required and unique, and no unlock check is applied.</para>
    /// <para>Code is the natural key, so a taken code answers 409 with a <see cref="TemplateNameConflict"/> reporting the existing template and how many
    /// units are built from it. Re-sending the same body with <c>OverwriteExisting</c> set performs the overwrite and answers 200. The caller never
    /// needs the existing template's id — that is what separates this from the by-id update below.</para>
    /// </remarks>
    /// <response code="200">An existing template was overwritten (the request set OverwriteExisting); returns the updated template</response>
    /// <response code="201">The template was created; returns the created template</response>
    /// <response code="400">Validation error, or the assembled design fails slot-acceptance rules</response>
    /// <response code="404">The base or a fitted component code does not exist in the catalogue</response>
    /// <response code="409">A template with the same code already exists and the save did not confirm the overwrite</response>
    /// <response code="500">Returns an error response with an array of errors in the event of an exception or other server error</response>
    [Authorize(Policy = nameof(PermissionType.EntitySetupTool))]
    [HttpPost]
    [ProducesResponseType(typeof(GlobalUnitTemplateView), 200)]
    [ProducesResponseType(typeof(GlobalUnitTemplateView), 201)]
    [ProducesResponseType(typeof(IApiErrorResult), 400)]
    [ProducesResponseType(typeof(IApiErrorResult), 404)]
    [ProducesResponseType(typeof(TemplateNameConflict), 409)]
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
    /// Dev design tool: update an existing global/common unit template in the shared catalogue.
    /// </summary>
    /// <remarks>
    /// Gated behind <see cref="PermissionType.EntitySetupTool"/>, since it writes the shared template catalogue.
    /// </remarks>
    /// <response code="200">The template was updated; returns the updated template</response>
    /// <response code="400">Validation error, or the assembled design fails slot-acceptance rules</response>
    /// <response code="404">No global template exists with the given id, or a referenced component code does not exist</response>
    /// <response code="409">Another template already uses the requested code</response>
    /// <response code="500">Returns an error response with an array of errors in the event of an exception or other server error</response>
    [Authorize(Policy = nameof(PermissionType.EntitySetupTool))]
    [HttpPut("{id:int}")]
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
    /// <para>
    /// Gated behind <see cref="PermissionType.EntitySetupTool"/>, since it rewrites every compiled snapshot in the database.
    /// </para>
    /// <para>
    /// Despite living on this controller, this action is not global-only: it recompiles player-owned custom templates as well. It sits here because it is the dev catalogue's maintenance action and shares the same permission gate, not because its scope is limited to global templates.
    /// </para>
    /// <para>
    /// Use after editing catalogue components, whose changes don't propagate to the snapshots of templates already built with them.
    /// Best-effort per template: failures are reported in the result without blocking the rest.
    /// </para>
    /// </remarks>
    /// <response code="200">The recompilation ran; returns the recompiled count and any per-template failures</response>
    /// <response code="500">Returns an error response with an array of errors in the event of an exception or other server error</response>
    [Authorize(Policy = nameof(PermissionType.EntitySetupTool))]
    [HttpPost("recompile-all")]
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
