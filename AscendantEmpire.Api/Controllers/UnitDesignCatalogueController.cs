using AscendantEmpire.Api.Utilities;
using AscendantEmpire.Application.Features.UnitComponents.Handlers;
using AscendantEmpire.BackendContracts.UnitDesigns.GetComponent;
using AscendantEmpire.BackendContracts.UnitDesigns.GetFullCatalog;
using AscendantEmpire.BackendContracts.UnitDesigns.UpsertUnitComponent;
using AscendantEmpire.Domain.Enums.Authentication;
using AscendantEmpire.FrontendContracts;

using MediatR;

using Microsoft.AspNetCore.Authorization;
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

    /// <summary>
    /// Dev design tool: get a single component in full (its body/weapon/armour extension, slots, tags and supersession links) by code, for the edit flow.
    /// </summary>
    /// <remarks>
    /// TODO: lock down before any non-dev deployment. 
    /// This endpoint is intentionally left unauthorized for now; it must be gated behind a dev/admin policy (or mapped only in Development) before shipping, 
    /// since it exposes the whole catalog.
    /// </remarks>
    /// <response code="200">The full component</response>
    /// <response code="404">No component exists with the given code</response>
    /// <response code="500">Returns an error response with an array of errors in the event of an exception or other server error</response>
    [HttpGet("{code}")]
    [ProducesResponseType(typeof(EditableComponentResult), 200)]
    [ProducesResponseType(typeof(IApiErrorResult), 404)]
    [ProducesResponseType(typeof(IApiErrorResult), 500)]
    public async Task<IActionResult> GetComponentAsync(
        [FromServices] IMediator mediator,
        string code,
        CancellationToken cancelToken)
    {
        ServiceResult<EditableComponentResult> response = await mediator.Send(new GetComponentRequest(code), cancelToken);

        return response.ToActionResult();
    }

    /// <summary>
    /// Dev design tool: create a single unit component in the global catalog.
    /// </summary>
    /// <remarks>
    /// The granular counterpart to <c>POST /unit-design/components/bulk</c>, which exists for catalogue transfer. A single edit
    /// shouldn't have to be expressed as a catalogue import — both share the same reconciler, so the graph
    /// rules are identical either way.
    /// </remarks>
    /// <response code="201">The component was created; returns the created component</response>
    /// <response code="400">Validation error</response>
    /// <response code="409">A component with the same code already exists</response>
    /// <response code="500">Returns an error response with an array of errors in the event of an exception or other server error</response>
    [Authorize(Policy = nameof(PermissionType.EntitySetupTool))]
    [HttpPost]
    [ProducesResponseType(typeof(UnitComponentView), 201)]
    [ProducesResponseType(typeof(IApiErrorResult), 400)]
    [ProducesResponseType(typeof(IApiErrorResult), 409)]
    [ProducesResponseType(typeof(IApiErrorResult), 500)]
    public async Task<IActionResult> CreateComponentAsync(
        [FromServices] IMediator mediator,
        [FromBody] UpsertUnitComponentDto request,
        CancellationToken cancelToken)
    {
        ServiceResult<UnitComponentView> response = await mediator.Send(new CreateUnitComponentRequest(request), cancelToken);

        return response.ToActionResult();
    }
}
