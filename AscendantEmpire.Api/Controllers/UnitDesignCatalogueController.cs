using AscendantEmpire.Api.Utilities;
using AscendantEmpire.Application.Features.UnitComponents.Classes;
using AscendantEmpire.Application.Features.UnitComponents.Handlers;
using AscendantEmpire.BackendContracts.UnitDesigns.BulkUpsertUnitComponents;
using AscendantEmpire.BackendContracts.UnitDesigns.GetComponent;
using AscendantEmpire.BackendContracts.UnitDesigns.GetFullCatalog;
using AscendantEmpire.BackendContracts.UnitDesigns.UpsertUnitComponent;
using AscendantEmpire.Domain.Enums.Authentication;
using AscendantEmpire.FrontendContracts;
using AscendantEmpire.FrontendContracts.UnitDesigns.BulkUpsertUnitComponents;

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
    /// The granular counterpart to <c>bulk</c>, which exists for catalogue transfer. A single edit
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

    /// <summary>
    /// Dev design tool: update a single existing unit component in the global catalog.
    /// </summary>
    /// <remarks>
    /// Child collections are reconciled rather than replaced, so slots that survive the edit keep their ids and
    /// the templates fitted into them keep working. A slot dropped from the payload is deleted together with
    /// its fittings, which the response reports so the loss isn't silent.
    /// </remarks>
    /// <response code="200">The component was updated; returns the updated component</response>
    /// <response code="400">Validation error, or the route code disagrees with the payload code</response>
    /// <response code="404">No component exists with the given code</response>
    /// <response code="500">Returns an error response with an array of errors in the event of an exception or other server error</response>
    [Authorize(Policy = nameof(PermissionType.EntitySetupTool))]
    [HttpPut("{code}")]
    [ProducesResponseType(typeof(UnitComponentView), 200)]
    [ProducesResponseType(typeof(IApiErrorResult), 400)]
    [ProducesResponseType(typeof(IApiErrorResult), 404)]
    [ProducesResponseType(typeof(IApiErrorResult), 500)]
    public async Task<IActionResult> UpdateComponentAsync(
        [FromServices] IMediator mediator,
        string code,
        [FromBody] UpsertUnitComponentDto request,
        CancellationToken cancelToken)
    {
        ServiceResult<UnitComponentView> response = await mediator.Send(new UpdateUnitComponentRequest(code, request), cancelToken);

        return response.ToActionResult();
    }

    /// <summary>
    /// Bulk-upsert unit components into the global catalog.
    /// </summary>
    /// <remarks>
    /// Writes the full component graph in one transaction. Components whose Code is new are inserted; components whose Code already exists are updated in place.
    /// The response reports how many were added vs updated. A component's type cannot be changed by an update.
    /// </remarks>
    /// <response code="200">Existing components were updated; returns the added/updated summary</response>
    /// <response code="201">All components were newly inserted; returns the added/updated summary</response>
    /// <response code="400">Validation error</response>
    /// <response code="500">Returns an error response with an array of errors in the event of an exception or other server error</response>
    [Authorize(Policy = nameof(PermissionType.EntitySetupTool))]
    [HttpPost("bulk")]
    [ProducesResponseType(typeof(IBulkUpsertUnitComponentsResult), 200)]
    [ProducesResponseType(typeof(IBulkUpsertUnitComponentsResult), 201)]
    [ProducesResponseType(typeof(IApiErrorResult), 400)]
    [ProducesResponseType(typeof(IApiErrorResult), 500)]
    public async Task<IActionResult> BulkUpsertComponentsAsync(
        [FromServices] IMediator mediator,
        [FromBody] BulkUpsertUnitComponentsDto request,
        CancellationToken cancelToken)
    {
        ServiceResult<BulkUpsertUnitComponentsResult> response = await mediator.Send(new BulkUpsertUnitComponentsRequest(request), cancelToken);

        return response.ToActionResult(result => new DTBulkUpsertUnitComponentsResult
        {
            AddedCount = result.AddedCount,
            UpdatedCount = result.UpdatedCount,
            UpdatedCodes = result.UpdatedCodes,
            RemovedSlotCount = result.RemovedSlotCount,
            UnfittedTemplateNames = result.UnfittedTemplateNames,
        });
    }
}
