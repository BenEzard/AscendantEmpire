using AscendantEmpire.Api.Utilities;
using AscendantEmpire.Application.Common;
using AscendantEmpire.Application.Data;
using AscendantEmpire.Application.Features.UnitComponents.Classes;
using AscendantEmpire.Application.Features.UnitComponents.Handlers;
using AscendantEmpire.Application.Options;
using AscendantEmpire.BackendContracts.UnitDesigns.BulkUpsertUnitComponents;
using AscendantEmpire.BackendContracts.UnitDesigns.CatalogTransfer;
using AscendantEmpire.BackendContracts.UnitDesigns.GetCompiledUnitTemplate;
using AscendantEmpire.BackendContracts.UnitDesigns.GetComponent;
using AscendantEmpire.BackendContracts.UnitDesigns.GetFullCatalog;
using AscendantEmpire.BackendContracts.UnitDesigns.GetPlayerTemplates;
using AscendantEmpire.BackendContracts.UnitDesigns.GetSlotCandidateComponents;
using AscendantEmpire.BackendContracts.UnitDesigns.GetTags;
using AscendantEmpire.BackendContracts.UnitDesigns.GetTagTypes;
using AscendantEmpire.BackendContracts.UnitDesigns.GetUnlockedBodies;
using AscendantEmpire.BackendContracts.UnitDesigns.RecompileAllUnitTemplates;
using AscendantEmpire.BackendContracts.UnitDesigns.SaveGlobalUnitTemplate;
using AscendantEmpire.BackendContracts.UnitDesigns.SaveUnitTemplate;
using AscendantEmpire.BackendContracts.UnitDesigns.UpsertTag;
using AscendantEmpire.BackendContracts.UnitDesigns.UpsertTagType;
using AscendantEmpire.BackendContracts.UnitDesigns.UpsertUnitComponent;
using AscendantEmpire.Domain.Enums.Authentication;
using AscendantEmpire.Domain.Enums.Processing;
using AscendantEmpire.Domain.Enums.System;
using AscendantEmpire.FrontendContracts;
using AscendantEmpire.FrontendContracts.UnitDesigns.BulkUpsertUnitComponents;

using MediatR;

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

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
    [Authorize(Policy = nameof(PermissionType.EntitySetupTool))]
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
            RemovedSlotCount = result.RemovedSlotCount,
            UnfittedTemplateNames = result.UnfittedTemplateNames,
        });
    }

    /// <summary>
    /// Dev design tool: create a single unit component in the global catalog.
    /// </summary>
    /// <remarks>
    /// The granular counterpart to <c>components/bulk</c>, which exists for catalogue transfer. A single edit
    /// shouldn't have to be expressed as a catalogue import — both share the same reconciler, so the graph
    /// rules are identical either way.
    /// </remarks>
    /// <response code="201">The component was created; returns the created component</response>
    /// <response code="400">Validation error</response>
    /// <response code="409">A component with the same code already exists</response>
    /// <response code="500">Returns an error response with an array of errors in the event of an exception or other server error</response>
    [Authorize(Policy = nameof(PermissionType.EntitySetupTool))]
    [HttpPost("components")]
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
    [HttpPut("components/{code}")]
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

        ServiceResult<UnlockedBodiesResult> response = await mediator.Send(new GetUnlockedBodiesRequest(gameId, playerId), cancelToken);

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

        ServiceResult<PlayerTemplatesResult> response = await mediator.Send(new GetPlayerTemplatesRequest(gameId, playerId), cancelToken);

        return response.ToActionResult();
    }

    /// <summary>
    /// Create a new custom (player-owned) unit template from an assembled design.
    /// </summary>
    /// <remarks>
    /// The game and player are read from the caller's game token, not the request. 
    /// The template and its compiled stats snapshot are persisted together, so the compiled read is available immediately after saving.
    /// </remarks>
    /// <response code="201">The template was created; returns the created template</response>
    /// <response code="400">Validation error, or the assembled design fails slot-acceptance rules</response>
    /// <response code="403">No game entered, or a referenced component has not been unlocked by the player</response>
    /// <response code="404">The base or a fitted component code does not exist in the catalog</response>
    /// <response code="500">Returns an error response with an array of errors in the event of an exception or other server error</response>
    [Authorize]
    [HttpPost("templates")]
    [ProducesResponseType(typeof(UnitTemplateView), 201)]
    [ProducesResponseType(typeof(IApiErrorResult), 400)]
    [ProducesResponseType(typeof(IApiErrorResult), 403)]
    [ProducesResponseType(typeof(IApiErrorResult), 404)]
    [ProducesResponseType(typeof(IApiErrorResult), 500)]
    public async Task<IActionResult> CreateUnitTemplateAsync(
        [FromServices] IMediator mediator,
        [FromBody] SaveUnitTemplateDto request,
        CancellationToken cancelToken)
    {
        if (!this.TryGetGameContext(out int gameId, out int playerId, out IActionResult? contextError))
        {
            return contextError!;
        }

        ServiceResult<UnitTemplateView> response = await mediator.Send(new CreateUnitTemplateRequest(gameId, playerId, request), cancelToken);

        return response.ToActionResult();
    }

    /// <summary>
    /// Update an existing custom (player-owned) unit template, replacing its base and fitted components.
    /// </summary>
    /// <remarks>
    /// The game and player are read from the caller's game token, not the request. Only the template's own
    /// owner may update it; any other caller (or an unknown id) gets a not-found.
    /// </remarks>
    /// <response code="200">The template was updated; returns the updated template</response>
    /// <response code="400">Validation error, or the assembled design fails slot-acceptance rules</response>
    /// <response code="403">No game entered, or a referenced component has not been unlocked by the player</response>
    /// <response code="404">No custom template exists with the given id (owned by the caller), or a referenced component code does not exist</response>
    /// <response code="500">Returns an error response with an array of errors in the event of an exception or other server error</response>
    [Authorize]
    [HttpPut("templates/{templateId:int}")]
    [ProducesResponseType(typeof(UnitTemplateView), 200)]
    [ProducesResponseType(typeof(IApiErrorResult), 400)]
    [ProducesResponseType(typeof(IApiErrorResult), 403)]
    [ProducesResponseType(typeof(IApiErrorResult), 404)]
    [ProducesResponseType(typeof(IApiErrorResult), 500)]
    public async Task<IActionResult> UpdateUnitTemplateAsync(
        [FromServices] IMediator mediator,
        int templateId,
        [FromBody] SaveUnitTemplateDto request,
        CancellationToken cancelToken)
    {
        if (!this.TryGetGameContext(out int gameId, out int playerId, out IActionResult? contextError))
        {
            return contextError!;
        }

        ServiceResult<UnitTemplateView> response =
            await mediator.Send(new UpdateUnitTemplateRequest(gameId, playerId, templateId, request), cancelToken);

        return response.ToActionResult();
    }

    /// <summary>
    /// Get the compiled snapshot of a saved template (aggregated stats, resolved components and capabilities).
    /// </summary>
    /// <remarks>
    /// Not unlock-filtered: the snapshot may contain components the caller never unlocked, so it serves both the player's own designs and enemy designs.
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

    /// <summary>
    /// Dev design tool: get a single component in full (its body/weapon/armour extension, slots, tags and supersession links) by code, for the edit flow.
    /// </summary>
    /// <remarks>
    /// TODO: lock down before any non-dev deployment. This endpoint is intentionally left unauthorized for now;
    /// it must be gated behind a dev/admin policy (or mapped only in Development) before shipping, since it exposes the whole catalog.
    /// </remarks>
    /// <response code="200">The full component</response>
    /// <response code="404">No component exists with the given code</response>
    /// <response code="500">Returns an error response with an array of errors in the event of an exception or other server error</response>
    [HttpGet("components/{code}")]
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
    /// Dev design tool: create a new global/common unit template in the shared catalog.
    /// </summary>
    /// <remarks>
    /// TODO: lock down before any non-dev deployment. This endpoint is intentionally left unauthorized for now;
    /// it must be gated behind a dev/admin policy (or mapped only in Development) before shipping, since it writes the shared template catalog.
    /// Unlike the player-facing create endpoint, this template has no game/player, its Code is required and unique, and no unlock check is applied.
    /// </remarks>
    /// <response code="201">The template was created; returns the created template</response>
    /// <response code="400">Validation error, or the assembled design fails slot-acceptance rules</response>
    /// <response code="404">The base or a fitted component code does not exist in the catalog</response>
    /// <response code="409">A template with the same code already exists</response>
    /// <response code="500">Returns an error response with an array of errors in the event of an exception or other server error</response>
    [Authorize(Policy = nameof(PermissionType.EntitySetupTool))]
    [HttpPost("templates/global")]
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

    /// <summary>
    /// Dev design tool: update an existing global/common unit template in the shared catalog.
    /// </summary>
    /// <remarks>
    /// TODO: lock down before any non-dev deployment. This endpoint is intentionally left unauthorized for now;
    /// it must be gated behind a dev/admin policy (or mapped only in Development) before shipping, since it writes the shared template catalog.
    /// </remarks>
    /// <response code="200">The template was updated; returns the updated template</response>
    /// <response code="400">Validation error, or the assembled design fails slot-acceptance rules</response>
    /// <response code="404">No global template exists with the given id, or a referenced component code does not exist</response>
    /// <response code="409">Another template already uses the requested code</response>
    /// <response code="500">Returns an error response with an array of errors in the event of an exception or other server error</response>
    [Authorize(Policy = nameof(PermissionType.EntitySetupTool))]
    [HttpPut("templates/global/{id:int}")]
    [ProducesResponseType(typeof(GlobalUnitTemplateView), 200)]
    [ProducesResponseType(typeof(IApiErrorResult), 400)]
    [ProducesResponseType(typeof(IApiErrorResult), 404)]
    [ProducesResponseType(typeof(IApiErrorResult), 409)]
    [ProducesResponseType(typeof(IApiErrorResult), 500)]
    public async Task<IActionResult> UpdateGlobalUnitTemplateAsync(
        [FromServices] IMediator mediator,
        int id,
        [FromBody] SaveGlobalUnitTemplateDto request,
        CancellationToken cancelToken)
    {
        ServiceResult<GlobalUnitTemplateView> response =
            await mediator.Send(new UpdateGlobalUnitTemplateRequest(id, request), cancelToken);

        return response.ToActionResult();
    }

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

    /// <summary>
    /// Dev design tool: get every tag in the global catalog, unfiltered.
    /// </summary>
    /// <remarks>
    /// TODO: lock down before any non-dev deployment. This endpoint is intentionally left unauthorized for now;
    /// it must be gated behind a dev/admin policy (or mapped only in Development) before shipping, since it exposes the whole tag catalog.
    /// </remarks>
    /// <response code="200">The full set of tags</response>
    /// <response code="500">Returns an error response with an array of errors in the event of an exception or other server error</response>
    [HttpGet("tags")]
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
    /// TODO: lock down before any non-dev deployment. This endpoint is intentionally left unauthorized for now;
    /// it must be gated behind a dev/admin policy (or mapped only in Development) before shipping, since it writes the global tag catalog.
    /// </remarks>
    /// <response code="201">The tag was created; returns the created tag</response>
    /// <response code="400">Validation error</response>
    /// <response code="409">A tag with the same code already exists</response>
    /// <response code="500">Returns an error response with an array of errors in the event of an exception or other server error</response>
    [Authorize(Policy = nameof(PermissionType.EntitySetupTool))]
    [HttpPost("tags")]
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
    /// Dev design tool: update an existing tag in the global catalog.
    /// </summary>
    /// <remarks>
    /// TODO: lock down before any non-dev deployment. This endpoint is intentionally left unauthorized for now;
    /// it must be gated behind a dev/admin policy (or mapped only in Development) before shipping, since it writes the global tag catalog.
    /// </remarks>
    /// <response code="200">The tag was updated; returns the updated tag</response>
    /// <response code="400">Validation error</response>
    /// <response code="404">No tag exists with the given id</response>
    /// <response code="409">Another tag already uses the requested code</response>
    /// <response code="500">Returns an error response with an array of errors in the event of an exception or other server error</response>
    [Authorize(Policy = nameof(PermissionType.EntitySetupTool))]
    [HttpPut("tags/{id:int}")]
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
    [HttpGet("tag-types")]
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
    /// Dev design tool: bulk-upsert the tag type catalog (create missing rows, update existing ones).
    /// </summary>
    /// <remarks>
    /// TODO: lock down before any non-dev deployment. This endpoint is intentionally left unauthorized for now;
    /// it must be gated behind a dev/admin policy (or mapped only in Development) before shipping, since it writes the global tag type catalog.
    /// </remarks>
    /// <response code="204">The tag types were upserted</response>
    /// <response code="400">Validation error</response>
    /// <response code="500">Returns an error response with an array of errors in the event of an exception or other server error</response>
    [Authorize(Policy = nameof(PermissionType.EntitySetupTool))]
    [HttpPut("tag-types")]
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

    /// <summary>
    /// Dev design tool: initiate an export of selected parts of the unit-design catalogue.
    /// The export runs asynchronously. Poll <c>GET /system/status?processId=X</c> for progress.
    /// Once complete, download via <c>GET /unit-design/export/download?processId=X</c>.
    /// </summary>
    /// <remarks>
    /// TODO: lock down before any non-dev deployment. Left unauthorized for now, consistent with the other
    /// dev catalog endpoints; it must be gated behind a dev/admin policy before shipping.
    /// </remarks>
    /// <response code="201">Export job accepted. Returns the processId.</response>
    /// <response code="400">Nothing was selected to export.</response>
    /// <response code="500">Server error.</response>
    [Authorize(Policy = nameof(PermissionType.SystemPanel))]
    [HttpPost("export")]
    [ProducesResponseType(typeof(int), 201)]
    [ProducesResponseType(typeof(IApiErrorResult), 400)]
    [ProducesResponseType(typeof(IApiErrorResult), 500)]
    public async Task<IActionResult> InitiateCatalogExportAsync(
        [FromServices] IMediator mediator,
        [FromBody] InitiateCatalogExportDto request,
        CancellationToken cancelToken)
    {
        ServiceResult<int> response = await mediator.Send(new InitiateCatalogExportRequest(ExportComponents: request.Components, ExportTemplates: request.Templates), cancelToken);

        return response.ToActionResult();
    }

    /// <summary>
    /// Dev design tool: download the completed catalogue export zip.
    /// Returns 404 if the export has not yet completed or the processId is not found.
    /// </summary>
    /// <response code="200">Returns the export zip file.</response>
    /// <response code="404">Export not ready or processId not found.</response>
    /// <response code="500">Server error.</response>
    [Authorize(Policy = nameof(PermissionType.SystemPanel))]
    [HttpGet("export/download")]
    [ProducesResponseType(200)]
    [ProducesResponseType(typeof(IApiErrorResult), 404)]
    [ProducesResponseType(typeof(IApiErrorResult), 500)]
    public async Task<IActionResult> DownloadCatalogExportAsync(
        [FromServices] IDbContextFactory<AscendantEmpireContext> contextFactory,
        [FromQuery] int processId,
        CancellationToken cancellationToken)
    {
        try
        {
            await using AscendantEmpireContext context = await contextFactory.CreateDbContextAsync(cancellationToken);

            var processing = await context.Processing
                .AsNoTracking()
                .FirstOrDefaultAsync(
                    p => p.ProcessingId == processId && p.ProcessType == ProcessType.ExportCatalog,
                    cancellationToken);

            if (processing is null || processing.Status != ProcessingStatus.Complete || string.IsNullOrEmpty(processing.ResultReference))
            {
                return new ObjectResult(new DTApiErrorResult(["Export not ready or processId not found."]))
                {
                    StatusCode = StatusCodes.Status404NotFound
                };
            }

            if (!System.IO.File.Exists(processing.ResultReference))
            {
                return new ObjectResult(new DTApiErrorResult(["Export file no longer available on the server."]))
                {
                    StatusCode = StatusCodes.Status404NotFound
                };
            }

            string fileName = Path.GetFileName(processing.ResultReference);
            Stream stream = new FileStream(
                path: processing.ResultReference,
                mode: FileMode.Open,
                access: FileAccess.Read,
                share: FileShare.Read,
                bufferSize: 65536,
                useAsync: true);

            return new FileStreamResult(stream, "application/zip")
            {
                FileDownloadName = fileName
            };
        }
        catch (Exception)
        {
            return new ObjectResult(new DTApiErrorResult(["An error occurred while retrieving the export file."]))
            {
                StatusCode = StatusCodes.Status500InternalServerError
            };
        }
    }

    /// <summary>
    /// Dev design tool: import a unit-design catalogue from a previously exported zip file.
    /// The uploaded zip is staged on disk and the import runs asynchronously.
    /// Poll <c>GET /system/status?processId=X</c> for progress.
    /// </summary>
    /// <remarks>
    /// TODO: lock down before any non-dev deployment. Left unauthorized for now, consistent with the other
    /// dev catalog endpoints; it must be gated behind a dev/admin policy before shipping, since it writes the shared catalog.
    /// </remarks>
    /// <response code="201">Import job accepted. Returns the processId.</response>
    /// <response code="400">No file supplied or the file is empty.</response>
    /// <response code="500">Server error.</response>
    [Authorize(Policy = nameof(PermissionType.SystemPanel))]
    [HttpPost("import")]
    [RequestSizeLimit(50 * 1024 * 1024)]
    [ProducesResponseType(typeof(int), 201)]
    [ProducesResponseType(typeof(IApiErrorResult), 400)]
    [ProducesResponseType(typeof(IApiErrorResult), 500)]
    public async Task<IActionResult> ImportCatalogAsync(
        [FromServices] IMediator mediator,
        [FromServices] IOptions<ImportSettings> importOptions,
        IFormFile file,
        CancellationToken cancellationToken)
    {
        if (file is null || file.Length == 0)
        {
            return new ObjectResult(new DTApiErrorResult(["No import file was supplied."]))
            {
                StatusCode = StatusCodes.Status400BadRequest
            };
        }

        // Stage the upload to the import temp directory under a unique name before queueing the job.
        string tempDirectory = importOptions.Value.TempDirectory;
        Directory.CreateDirectory(tempDirectory);
        string stagedPath = Path.Combine(tempDirectory, $"import_{Guid.NewGuid():N}.zip");

        await using (FileStream stagedStream = new(stagedPath, FileMode.Create, FileAccess.Write, FileShare.None))
        {
            await file.CopyToAsync(stagedStream, cancellationToken);
        }

        ServiceResult<int> response = await mediator.Send(new InitiateCatalogImportRequest(ZipPath: stagedPath), cancellationToken);

        return response.ToActionResult();
    }
}
