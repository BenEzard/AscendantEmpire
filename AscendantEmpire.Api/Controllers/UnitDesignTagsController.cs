using AscendantEmpire.Api.Utilities;
using AscendantEmpire.Application.Features.UnitComponents.Handlers;
using AscendantEmpire.BackendContracts.UnitDesigns.GetTags;
using AscendantEmpire.FrontendContracts;

using MediatR;

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
}
