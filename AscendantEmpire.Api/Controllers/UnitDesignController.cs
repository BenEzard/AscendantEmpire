using AscendantEmpire.Api.Utilities;
using AscendantEmpire.Application.Common;
using AscendantEmpire.Application.Features.UnitComponents.Handlers;
using AscendantEmpire.BackendContracts.UnitDesigns.RecompileAllUnitTemplates;
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
