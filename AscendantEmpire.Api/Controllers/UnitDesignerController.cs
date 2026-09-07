using AscendantEmpire.Api.Utilities;
using AscendantEmpire.Application.Common;
using AscendantEmpire.Application.Common.Messaging;
using AscendantEmpire.Application.Features.UnitComponents.Handlers;
using AscendantEmpire.BackendContracts.UnitDesigns.GetCompiledUnitTemplate;
using AscendantEmpire.BackendContracts.UnitDesigns.GetPlayerTemplates;
using AscendantEmpire.BackendContracts.UnitDesigns.GetSlotCandidateComponents;
using AscendantEmpire.BackendContracts.UnitDesigns.GetUnlockedBodies;
using AscendantEmpire.BackendContracts.UnitDesigns.SaveUnitTemplate;
using AscendantEmpire.BackendContracts.UnitDesigns.Shared;
using AscendantEmpire.FrontendContracts;

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

using static AscendantEmpire.Application.Common.ServiceResultBase;

namespace AscendantEmpire.Api.Controllers;

/// <summary>
/// The player-facing assembly bench: the unlock-scoped component feeds a design is built from, and the custom templates a player saves out of it.
/// </summary>
/// <remarks>
/// <para>
/// Every action here serves the one Unit Designer screen and shares a single authorization posture — a plain <c>[Authorize]</c> with no permission policy — so this controller is reachable by any authenticated player, unlike the dev-tool controllers the rest of the unit-design surface was split into.
/// </para>
/// <para>
/// Identity is never taken from the request. Every action that needs a game and player reads them from the caller's game token via <c>TryGetGameContext</c>, which answers 403 when no game has been entered; ids carried in a route or body would let a caller read or write another player's designs.
/// </para>
/// <para>
/// The compiled-snapshot read is the single exception, and deliberately so: it takes no game context because it is not unlock-filtered, serving enemy designs as readily as the caller's own.
/// </para>
/// </remarks>
[ApiController]
[Route("[controller]")]
public class UnitDesignerController : ControllerBase
{
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
    /// Get the unlocked components that fit a given slot of a given body (the component-selection feed).
    /// </summary>
    /// <remarks>
    /// The game and player are read from the caller's game token, not the request. A slot's owner is always a body — the only component type that exposes slots — which is why both the route segment and the parameter name say body; <c>GetSlotCandidateComponentsHandler</c> enforces it by matching the slot on <c>UnitComponentBodyId</c>.
    /// </remarks>
    /// <response code="200">The unlocked components that satisfy the slot's rules</response>
    /// <response code="403">No game entered (the token has no game context)</response>
    /// <response code="404">The slot does not belong to the given body</response>
    /// <response code="500">Returns an error response with an array of errors in the event of an exception or other server error</response>
    [Authorize]
    [HttpGet("bodies/{bodyComponentId:int}/slots/{slotId:int}/components")]
    [ProducesResponseType(typeof(SlotCandidateComponentsResult), 200)]
    [ProducesResponseType(typeof(IApiErrorResult), 403)]
    [ProducesResponseType(typeof(IApiErrorResult), 404)]
    [ProducesResponseType(typeof(IApiErrorResult), 500)]
    public async Task<IActionResult> GetSlotCandidateComponentsAsync(
        [FromServices] IMediator mediator,
        int bodyComponentId,
        int slotId,
        CancellationToken cancelToken)
    {
        if (!this.TryGetGameContext(out int gameId, out int playerId, out IActionResult? contextError))
        {
            return contextError!;
        }

        ServiceResult<SlotCandidateComponentsResult> response =
            await mediator.Send(new GetSlotCandidateComponentsRequest(gameId, playerId, bodyComponentId, slotId), cancelToken);

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
    /// Save a custom (player-owned) unit template from an assembled design, creating it or — on confirmation — overwriting the caller's design that
    /// already holds its name.
    /// </summary>
    /// <remarks>
    /// <para>The game and player are read from the caller's game token, not the request.
    /// The template and its compiled stats snapshot are persisted together, so the compiled read is available immediately after saving.</para>
    /// <para>A name is a player's natural key for their own designs, so a taken name answers 409 with a <see cref="TemplateNameConflict"/>. Its
    /// <c>CanOverwrite</c> says whether re-sending with <c>OverwriteExisting</c> set would help: it would not when the name belongs to a global/common
    /// design, nor when the caller's own design already has Units in the field. The caller never needs the existing design's id — that is what
    /// separates this from the by-id update below.</para>
    /// </remarks>
    /// <response code="200">An existing design was overwritten (the request set OverwriteExisting); returns the updated template</response>
    /// <response code="201">The template was created; returns the created template</response>
    /// <response code="400">Validation error, or the assembled design fails slot-acceptance rules</response>
    /// <response code="403">No game entered, or a referenced component has not been unlocked by the player</response>
    /// <response code="404">The base or a fitted component code does not exist in the catalogue</response>
    /// <response code="409">The name is already taken; the body's Detail says whether confirming an overwrite would resolve it</response>
    /// <response code="500">Returns an error response with an array of errors in the event of an exception or other server error</response>
    [Authorize]
    [HttpPost("templates")]
    [ProducesResponseType(typeof(UnitTemplateView), 200)]
    [ProducesResponseType(typeof(UnitTemplateView), 201)]
    [ProducesResponseType(typeof(IApiErrorResult), 400)]
    [ProducesResponseType(typeof(IApiErrorResult), 403)]
    [ProducesResponseType(typeof(IApiErrorResult), 404)]
    [ProducesResponseType(typeof(TemplateNameConflict), 409)]
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
    /// <response code="409">Units are already fielded from this design, or the requested name belongs to another design</response>
    /// <response code="500">Returns an error response with an array of errors in the event of an exception or other server error</response>
    [Authorize]
    [HttpPut("templates/{templateId:int}")]
    [ProducesResponseType(typeof(UnitTemplateView), 200)]
    [ProducesResponseType(typeof(IApiErrorResult), 400)]
    [ProducesResponseType(typeof(IApiErrorResult), 403)]
    [ProducesResponseType(typeof(IApiErrorResult), 404)]
    [ProducesResponseType(typeof(TemplateNameConflict), 409)]
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
    /// Not unlock-filtered, and the only action here that reads no game context: the snapshot may contain components the caller never unlocked, so it serves both the player's own designs and enemy designs.
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
}
