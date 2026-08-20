using AscendantEmpire.Api.Utilities;
using AscendantEmpire.Application.Features.UnitComponents.Handlers;
using AscendantEmpire.BackendContracts.UnitDesigns.GetTags;
using AscendantEmpire.BackendContracts.UnitDesigns.UpsertTag;
using AscendantEmpire.Domain.Enums.Authentication;
using AscendantEmpire.FrontendContracts;

using MediatR;

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

using static AscendantEmpire.Application.Common.ServiceResultBase;

namespace AscendantEmpire.Api.Controllers;

/// <summary>
/// The global unit-design tag catalogue and the editable metadata for the tag types that classify it, both authored through the dev Entity Setup Tool.
/// </summary>
[ApiController]
[Route("[controller]")]
public class UnitDesignTagsController : ControllerBase
{
    /// <summary>
    /// Dev design tool: get every tag in the global catalog, unfiltered.
    /// </summary>
    /// <remarks>
    /// TODO: lock down before any non-dev deployment. This endpoint is intentionally left unauthorized for now;
    /// it must be gated behind a dev/admin policy (or mapped only in Development) before shipping, since it exposes the whole tag catalog.
    /// </remarks>
    /// <response code="200">The full set of tags</response>
    /// <response code="500">Returns an error response with an array of errors in the event of an exception or other server error</response>
    [HttpGet]
    [ProducesResponseType(typeof(GetTagsResult), 200)]
    [ProducesResponseType(typeof(IApiErrorResult), 500)]
    public async Task<IActionResult> GetTagsAsync(
        [FromServices] IMediator mediator,
        CancellationToken cancelToken)
    {
        ServiceResult<GetTagsResult> response = await mediator.Send(new GetTagsRequest(), cancelToken);

        return response.ToActionResult();
    }

    /// <summary>
    /// Dev design tool: create a single tag in the global catalog.
    /// </summary>
    /// <remarks>
    /// Gated behind <see cref="PermissionType.EntitySetupTool"/>, since it writes the global tag catalog.
    /// </remarks>
    /// <response code="201">The tag was created; returns the created tag</response>
    /// <response code="400">Validation error</response>
    /// <response code="409">A tag with the same code already exists</response>
    /// <response code="500">Returns an error response with an array of errors in the event of an exception or other server error</response>
    [Authorize(Policy = nameof(PermissionType.EntitySetupTool))]
    [HttpPost]
    [ProducesResponseType(typeof(TagView), 201)]
    [ProducesResponseType(typeof(IApiErrorResult), 400)]
    [ProducesResponseType(typeof(IApiErrorResult), 409)]
    [ProducesResponseType(typeof(IApiErrorResult), 500)]
    public async Task<IActionResult> CreateTagAsync(
        [FromServices] IMediator mediator,
        [FromBody] UpsertTagDto request,
        CancellationToken cancelToken)
    {
        ServiceResult<TagView> response = await mediator.Send(new CreateTagRequest(request), cancelToken);

        return response.ToActionResult();
    }
}
