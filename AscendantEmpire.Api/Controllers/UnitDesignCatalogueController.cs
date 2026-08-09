using AscendantEmpire.Api.Utilities;
using AscendantEmpire.Application.Features.UnitComponents.Handlers;
using AscendantEmpire.BackendContracts.UnitDesigns.GetFullCatalog;
using AscendantEmpire.FrontendContracts;

using MediatR;

using Microsoft.AspNetCore.Mvc;

using static AscendantEmpire.Application.Common.ServiceResultBase;

namespace AscendantEmpire.Api.Controllers;

/// <summary>
/// The global unit-design component catalogue: the bodies, weapons, armour and equipment the dev Entity Setup Tool authors, before any per-player unlock filtering is applied.
/// </summary>
[ApiController]
[Route("[controller]")]
public class UnitDesignCatalogueController : ControllerBase
{
    /// <summary>
    /// Dev design tool: get the entire unit-design catalog unfiltered (every body, component, tag and template), ignoring game/player and unlock scoping.
    /// </summary>
    /// <remarks>
    /// TODO: lock down before any non-dev deployment. 
    /// This endpoint is intentionally left unauthorized for now; it must be gated behind a dev/admin policy (or mapped only in Development) before shipping, 
    /// since it exposes the whole catalog.
    /// </remarks>
    /// <response code="200">The full catalog</response>
    /// <response code="500">Returns an error response with an array of errors in the event of an exception or other server error</response>
    [HttpGet]
    [ProducesResponseType(typeof(FullCatalogResult), 200)]
    [ProducesResponseType(typeof(IApiErrorResult), 500)]
    public async Task<IActionResult> GetFullCatalogAsync(
        [FromServices] IMediator mediator,
        CancellationToken cancelToken)
    {
        ServiceResult<FullCatalogResult> response =
            await mediator.Send(new GetFullCatalogRequest(), cancelToken);

        return response.ToActionResult();
    }
}
