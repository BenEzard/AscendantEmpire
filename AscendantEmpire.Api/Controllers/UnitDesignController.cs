using AscendantEmpire.Api.Utilities;
using AscendantEmpire.Application.Features.UnitComponents.Classes;
using AscendantEmpire.Application.Features.UnitComponents.Handlers;
using AscendantEmpire.BackendContracts.UnitDesigns.BulkUpsertUnitComponents;
using AscendantEmpire.BackendContracts.UnitDesigns.GetCompiledUnitTemplate;
using AscendantEmpire.BackendContracts.UnitDesigns.GetFullCatalog;
using AscendantEmpire.BackendContracts.UnitDesigns.GetPlayerTemplates;
using AscendantEmpire.BackendContracts.UnitDesigns.GetSlotCandidateComponents;
using AscendantEmpire.BackendContracts.UnitDesigns.GetUnlockedBodies;
using AscendantEmpire.FrontendContracts;
using AscendantEmpire.FrontendContracts.UnitDesigns.BulkUpsertUnitComponents;

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
    [HttpPost("components/bulk")]
    [ProducesResponseType(typeof(IBulkUpsertUnitComponentsResult), 200)]
    [ProducesResponseType(typeof(IBulkUpsertUnitComponentsResult), 201)]
    [ProducesResponseType(typeof(IApiErrorResult), 400)]
    [ProducesResponseType(typeof(IApiErrorResult), 500)]
    public async Task<IActionResult> BulkUpsertComponentsAsync(
        [FromServices] IMediator mediator,
        [FromBody] BulkUpsertUnitComponentsDto request,
        CancellationToken cancelToken)
    {
        ServiceResult<BulkUpsertUnitComponentsResult> response =
            await mediator.Send(new BulkUpsertUnitComponentsRequest(request), cancelToken);

        return response.ToActionResult(result => new DTBulkUpsertUnitComponentsResult
        {
            AddedCount = result.AddedCount,
            UpdatedCount = result.UpdatedCount,
            UpdatedCodes = result.UpdatedCodes,
        });
    }

    /// <summary>
    /// Get the Body components the caller has unlocked, each with its slots and slot rules.
    /// </summary>
    /// <remarks>The game and player are read from the caller's game token, not the request.</remarks>
    /// <response code="200">The unlocked bodies and their slots</response>
    /// <response code="403">No game entered (the token has no game context)</response>
    /// <response code="500">Returns an error response with an array of errors in the event of an exception or other server error</response>
    [Authorize]
    [HttpGet("bodies")]
    [ProducesResponseType(typeof(UnlockedBodiesResult), 200)]
    [ProducesResponseType(typeof(IApiErrorResult), 403)]
    [ProducesResponseType(typeof(IApiErrorResult), 500)]
    public async Task<IActionResult> GetUnlockedBodiesAsync(
        [FromServices] IMediator mediator,
        CancellationToken cancelToken)
    {
        if (!this.TryGetGameContext(out int gameId, out int playerId, out IActionResult? contextError))
        {
            return contextError!;
        }

        ServiceResult<UnlockedBodiesResult> response =
            await mediator.Send(new GetUnlockedBodiesRequest(gameId, playerId), cancelToken);

        return response.ToActionResult();
    }

    /// <summary>
    /// Get the unlocked components that fit a given slot of a given owner component (the component-selection feed).
    /// </summary>
    /// <remarks>
    /// The game and player are read from the caller's game token, not the request. The owner component may
    /// be a body or any fitted component that exposes its own slots (nesting).
    /// </remarks>
    /// <response code="200">The unlocked components that satisfy the slot's rules</response>
    /// <response code="403">No game entered (the token has no game context)</response>
    /// <response code="404">The slot does not belong to the given owner component</response>
    /// <response code="500">Returns an error response with an array of errors in the event of an exception or other server error</response>
    [Authorize]
    [HttpGet("components/{ownerComponentId:int}/slots/{slotId:int}/components")]
    [ProducesResponseType(typeof(SlotCandidateComponentsResult), 200)]
    [ProducesResponseType(typeof(IApiErrorResult), 403)]
    [ProducesResponseType(typeof(IApiErrorResult), 404)]
    [ProducesResponseType(typeof(IApiErrorResult), 500)]
    public async Task<IActionResult> GetSlotCandidateComponentsAsync(
        [FromServices] IMediator mediator,
        int ownerComponentId,
        int slotId,
        CancellationToken cancelToken)
    {
        if (!this.TryGetGameContext(out int gameId, out int playerId, out IActionResult? contextError))
        {
            return contextError!;
        }

        ServiceResult<SlotCandidateComponentsResult> response =
            await mediator.Send(new GetSlotCandidateComponentsRequest(gameId, playerId, ownerComponentId, slotId), cancelToken);

        return response.ToActionResult();
    }

    /// <summary>
    /// Get the templates available to the caller: global/common templates they have unlocked plus their own custom templates.
    /// </summary>
    /// <remarks>The game and player are read from the caller's game token, not the request.</remarks>
    /// <response code="200">The player's available templates</response>
    /// <response code="403">No game entered (the token has no game context)</response>
    /// <response code="500">Returns an error response with an array of errors in the event of an exception or other server error</response>
    [Authorize]
    [HttpGet("templates")]
    [ProducesResponseType(typeof(PlayerTemplatesResult), 200)]
    [ProducesResponseType(typeof(IApiErrorResult), 403)]
    [ProducesResponseType(typeof(IApiErrorResult), 500)]
    public async Task<IActionResult> GetPlayerTemplatesAsync(
        [FromServices] IMediator mediator,
        CancellationToken cancelToken)
    {
        if (!this.TryGetGameContext(out int gameId, out int playerId, out IActionResult? contextError))
        {
            return contextError!;
        }

        ServiceResult<PlayerTemplatesResult> response =
            await mediator.Send(new GetPlayerTemplatesRequest(gameId, playerId), cancelToken);

        return response.ToActionResult();
    }

    /// <summary>
    /// Get the compiled snapshot of a finalized template (aggregated stats, resolved components and capabilities).
    /// </summary>
    /// <remarks>
    /// Not unlock-filtered: the snapshot may contain components the caller never unlocked, so it serves both the player's own finalized designs and enemy designs.
    /// </remarks>
    /// <response code="200">The compiled design snapshot</response>
    /// <response code="404">No compiled snapshot exists for the template</response>
    /// <response code="500">Returns an error response with an array of errors in the event of an exception or other server error</response>
    [Authorize]
    [HttpGet("templates/{templateId:int}/compiled")]
    [ProducesResponseType(typeof(CompiledUnitTemplateView), 200)]
    [ProducesResponseType(typeof(IApiErrorResult), 404)]
    [ProducesResponseType(typeof(IApiErrorResult), 500)]
    public async Task<IActionResult> GetCompiledUnitTemplateAsync(
        [FromServices] IMediator mediator,
        int templateId,
        CancellationToken cancelToken)
    {
        ServiceResult<CompiledUnitTemplateView> response =
            await mediator.Send(new GetCompiledUnitTemplateRequest(templateId), cancelToken);

        return response.ToActionResult();
    }

    /// <summary>
    /// Dev design tool: get the entire unit-design catalog unfiltered (every body, component, tag and template), ignoring game/player and unlock scoping.
    /// </summary>
    /// <remarks>
    /// TODO: lock down before any non-dev deployment. This endpoint is intentionally left unauthorized for now; 
    /// it must be gated behind a dev/admin policy (or mapped only in Development) before shipping, since it exposes the whole catalog.
    /// </remarks>
    /// <response code="200">The full catalog</response>
    /// <response code="500">Returns an error response with an array of errors in the event of an exception or other server error</response>
    [HttpGet("catalog")]
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
