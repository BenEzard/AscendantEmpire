using AscendantEmpire.Api.Utilities;
using AscendantEmpire.Application.Common;
using AscendantEmpire.Application.Common.Messaging;
using AscendantEmpire.Application.Features.Assets.Handlers;
using AscendantEmpire.BackendContracts.Assets.GetSupplySets;
using AscendantEmpire.BackendContracts.Assets.UpsertSupplySet;
using AscendantEmpire.Domain.Enums.Authentication;
using AscendantEmpire.FrontendContracts;

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

using static AscendantEmpire.Application.Common.ServiceResultBase;

namespace AscendantEmpire.Api.Controllers;

/// <summary>
/// The global supply set catalogue — the groups of Assets that satisfy a Unit's Food, Fuel and Repair needs, authored through the dev Entity Setup Tool.
/// </summary>
[ApiController]
[Route("[controller]")]
public class SupplySetsController : ControllerBase
{
    /// <summary>
    /// Dev design tool: get every supply set in the catalogue, with its members.
    /// </summary>
    /// <remarks>
    /// TODO: lock down before any non-dev deployment. This endpoint is intentionally left unauthorized for now;
    /// it must be gated behind a dev/admin policy (or mapped only in Development) before shipping, since it exposes the whole supply catalogue.
    /// </remarks>
    /// <response code="200">Every supply set, ordered by code</response>
    /// <response code="500">Returns an error response with an array of errors in the event of an exception or other server error</response>
    [HttpGet]
    [ProducesResponseType(typeof(GetSupplySetsResult), 200)]
    [ProducesResponseType(typeof(IApiErrorResult), 500)]
    public async Task<IActionResult> GetSupplySetsAsync(
        [FromServices] IMediator mediator,
        CancellationToken cancelToken)
    {
        ServiceResult<GetSupplySetsResult> response = await mediator.Send(new GetSupplySetsRequest(), cancelToken);

        return response.ToActionResult();
    }

    /// <summary>
    /// Dev design tool: create a single supply set.
    /// </summary>
    /// <remarks>
    /// Gated behind <see cref="PermissionType.EntitySetupTool"/>, since it writes the global supply catalogue.
    /// </remarks>
    /// <response code="201">The set was created; returns the created set</response>
    /// <response code="400">Validation error, or an Asset code that does not resolve</response>
    /// <response code="409">A supply set with the same code already exists</response>
    /// <response code="500">Returns an error response with an array of errors in the event of an exception or other server error</response>
    [Authorize(Policy = nameof(PermissionType.EntitySetupTool))]
    [HttpPost]
    [ProducesResponseType(typeof(SupplySetView), 201)]
    [ProducesResponseType(typeof(IApiErrorResult), 400)]
    [ProducesResponseType(typeof(IApiErrorResult), 409)]
    [ProducesResponseType(typeof(IApiErrorResult), 500)]
    public async Task<IActionResult> CreateSupplySetAsync(
        [FromServices] IMediator mediator,
        [FromBody] UpsertSupplySetDto request,
        CancellationToken cancelToken)
    {
        ServiceResult<SupplySetView> response = await mediator.Send(new CreateSupplySetRequest(request), cancelToken);

        return response.ToActionResult();
    }

    /// <summary>
    /// Dev design tool: update an existing supply set, reconciling its members to exactly the payload.
    /// </summary>
    /// <remarks>
    /// Gated behind <see cref="PermissionType.EntitySetupTool"/>, since it writes the global supply catalogue.
    /// </remarks>
    /// <response code="200">The set was updated; returns the updated set</response>
    /// <response code="400">Validation error, or an Asset code that does not resolve</response>
    /// <response code="404">No supply set exists with the given id</response>
    /// <response code="409">Another supply set already uses the requested code</response>
    /// <response code="500">Returns an error response with an array of errors in the event of an exception or other server error</response>
    [Authorize(Policy = nameof(PermissionType.EntitySetupTool))]
    [HttpPut("{id:int}")]
    [ProducesResponseType(typeof(SupplySetView), 200)]
    [ProducesResponseType(typeof(IApiErrorResult), 400)]
    [ProducesResponseType(typeof(IApiErrorResult), 404)]
    [ProducesResponseType(typeof(IApiErrorResult), 409)]
    [ProducesResponseType(typeof(IApiErrorResult), 500)]
    public async Task<IActionResult> UpdateSupplySetAsync(
        [FromServices] IMediator mediator,
        int id,
        [FromBody] UpsertSupplySetDto request,
        CancellationToken cancelToken)
    {
        ServiceResult<SupplySetView> response = await mediator.Send(new UpdateSupplySetRequest(id, request), cancelToken);

        return response.ToActionResult();
    }
}
