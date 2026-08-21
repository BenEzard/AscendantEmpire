using AscendantEmpire.Api.Utilities;
using AscendantEmpire.Application.Common;
using AscendantEmpire.Application.Features.UnitComponents.Handlers;
using AscendantEmpire.BackendContracts.UnitDesigns.SaveGlobalUnitTemplate;
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
/// Every action is gated behind <see cref="PermissionType.EntitySetupTool"/>, since all three write the shared catalogue rather than anything the caller owns.
/// </para>
/// </remarks>
[ApiController]
[Route("[controller]")]
public class GlobalUnitTemplatesController : ControllerBase
{
    /// <summary>
    /// Dev design tool: create a new global/common unit template in the shared catalog.
    /// </summary>
    /// <remarks>
    /// Gated behind <see cref="PermissionType.EntitySetupTool"/>, since it writes the shared template catalog.
    /// Unlike the player-facing create endpoint, this template has no game/player, its Code is required and unique, and no unlock check is applied.
    /// </remarks>
    /// <response code="201">The template was created; returns the created template</response>
    /// <response code="400">Validation error, or the assembled design fails slot-acceptance rules</response>
    /// <response code="404">The base or a fitted component code does not exist in the catalog</response>
    /// <response code="409">A template with the same code already exists</response>
    /// <response code="500">Returns an error response with an array of errors in the event of an exception or other server error</response>
    [Authorize(Policy = nameof(PermissionType.EntitySetupTool))]
    [HttpPost]
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
}
