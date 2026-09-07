using AscendantEmpire.Api.Utilities;
using AscendantEmpire.Application.Common;
using AscendantEmpire.Application.Common.Messaging;
using AscendantEmpire.Application.Features.UnitComponents.Handlers;
using AscendantEmpire.BackendContracts.UnitDesigns.GetTags;
using AscendantEmpire.BackendContracts.UnitDesigns.GetTagTypes;
using AscendantEmpire.BackendContracts.UnitDesigns.UpsertTag;
using AscendantEmpire.BackendContracts.UnitDesigns.UpsertTagType;
using AscendantEmpire.Domain.Enums.Authentication;
using AscendantEmpire.FrontendContracts;

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
    /// Dev design tool: get every tag in the global catalogue, unfiltered.
    /// </summary>
    /// <remarks>
    /// TODO: lock down before any non-dev deployment. This endpoint is intentionally left unauthorized for now;
    /// it must be gated behind a dev/admin policy (or mapped only in Development) before shipping, since it exposes the whole tag catalogue.
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
    /// Dev design tool: create a single tag in the global catalogue.
    /// </summary>
    /// <remarks>
    /// Gated behind <see cref="PermissionType.EntitySetupTool"/>, since it writes the global tag catalogue.
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

    /// <summary>
    /// Dev design tool: update an existing tag in the global catalogue.
    /// </summary>
    /// <remarks>
    /// <para>Gated behind <see cref="PermissionType.EntitySetupTool"/>, since it writes the global tag catalogue.</para>
    /// <para>
    /// The <c>:int</c> route constraint is load-bearing, not decoration. Without it this route also matches
    /// <c>PUT /unit-design-tags/types</c>, and the two become an ambiguous match — a 500, not a 404.
    /// </para>
    /// </remarks>
    /// <response code="200">The tag was updated; returns the updated tag</response>
    /// <response code="400">Validation error</response>
    /// <response code="404">No tag exists with the given id</response>
    /// <response code="409">Another tag already uses the requested code</response>
    /// <response code="500">Returns an error response with an array of errors in the event of an exception or other server error</response>
    [Authorize(Policy = nameof(PermissionType.EntitySetupTool))]
    [HttpPut("{id:int}")]
    [ProducesResponseType(typeof(TagView), 200)]
    [ProducesResponseType(typeof(IApiErrorResult), 400)]
    [ProducesResponseType(typeof(IApiErrorResult), 404)]
    [ProducesResponseType(typeof(IApiErrorResult), 409)]
    [ProducesResponseType(typeof(IApiErrorResult), 500)]
    public async Task<IActionResult> UpdateTagAsync(
        [FromServices] IMediator mediator,
        int id,
        [FromBody] UpsertTagDto request,
        CancellationToken cancelToken)
    {
        ServiceResult<TagView> response = await mediator.Send(new UpdateTagRequest(id, request), cancelToken);

        return response.ToActionResult();
    }

    /// <summary>
    /// Dev design tool: get every tag type's editable metadata.
    /// </summary>
    /// <remarks>
    /// TODO: lock down before any non-dev deployment. This endpoint is intentionally left unauthorized for now;
    /// it must be gated behind a dev/admin policy (or mapped only in Development) before shipping.
    /// </remarks>
    /// <response code="200">The full set of tag types</response>
    /// <response code="500">Returns an error response with an array of errors in the event of an exception or other server error</response>
    [HttpGet("types")]
    [ProducesResponseType(typeof(GetTagTypesResult), 200)]
    [ProducesResponseType(typeof(IApiErrorResult), 500)]
    public async Task<IActionResult> GetTagTypesAsync(
        [FromServices] IMediator mediator,
        CancellationToken cancelToken)
    {
        ServiceResult<GetTagTypesResult> response = await mediator.Send(new GetTagTypesRequest(), cancelToken);

        return response.ToActionResult();
    }

    /// <summary>
    /// Dev design tool: bulk-upsert the tag type catalogue (create missing rows, update existing ones).
    /// </summary>
    /// <remarks>
    /// <para>Gated behind <see cref="PermissionType.EntitySetupTool"/>, since it writes the global tag type catalogue.</para>
    /// <para>
    /// This route is the reason <see cref="UpdateTagAsync"/> constrains its id to <c>:int</c> — without that
    /// constraint, <c>types</c> would match both actions and the pair would be an ambiguous match.
    /// </para>
    /// </remarks>
    /// <response code="204">The tag types were upserted</response>
    /// <response code="400">Validation error</response>
    /// <response code="500">Returns an error response with an array of errors in the event of an exception or other server error</response>
    [Authorize(Policy = nameof(PermissionType.EntitySetupTool))]
    [HttpPut("types")]
    [ProducesResponseType(204)]
    [ProducesResponseType(typeof(IApiErrorResult), 400)]
    [ProducesResponseType(typeof(IApiErrorResult), 500)]
    public async Task<IActionResult> UpsertTagTypesAsync(
        [FromServices] IMediator mediator,
        [FromBody] IReadOnlyCollection<UpsertTagTypeDto> request,
        CancellationToken cancelToken)
    {
        ServiceResultBase response = await mediator.Send(new UpsertTagTypesRequest(request), cancelToken);

        return response.ToActionResult();
    }
}
