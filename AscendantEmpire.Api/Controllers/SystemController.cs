using AscendantEmpire.Api.Utilities;
using AscendantEmpire.Application.Common.Messaging;
using AscendantEmpire.Application.Features.DatabaseSeeding.Classes;
using AscendantEmpire.Application.Features.DatabaseSeeding.Handlers;
using AscendantEmpire.Application.Features.SystemMonitoring.Classes;
using AscendantEmpire.Application.Features.SystemMonitoring.Handlers;
using AscendantEmpire.BackendContracts.DatabaseSeeding;
using AscendantEmpire.Domain.Enums.Authentication;
using AscendantEmpire.FrontendContracts;

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

using static AscendantEmpire.Application.Common.ServiceResultBase;

namespace AscendantEmpire.Api.Controllers;

[ApiController]
[Route("[controller]")]
public class SystemController : ControllerBase
{
    /// <summary>
    /// Get the current status of a background process by its processId.
    /// </summary>
    /// <response code="200">Returns the current processing status.</response>
    /// <response code="404">ProcessId not found.</response>
    /// <response code="500">Server error.</response>
    [HttpGet("status")]
    [ProducesResponseType(typeof(DTProcessingStatus), 200)]
    [ProducesResponseType(typeof(IApiErrorResult), 404)]
    [ProducesResponseType(typeof(IApiErrorResult), 500)]
    public async Task<IActionResult> GetProcessingStatusAsync(
        [FromServices] IMediator mediator,
        [FromQuery] int processId,
        CancellationToken cancellationToken)
    {
        ServiceResult<DTProcessingStatus> response = await mediator.Send(
            new GetProcessingStatusRequest(ProcessingId: processId), cancellationToken);

        return response.ToActionResult();
    }

    /// <summary>
    /// Dev tool: lists the parts of the database that can be seeded, and which of them depend on which.
    /// </summary>
    /// <response code="200">The seed targets, ordered as the dialog lists them.</response>
    /// <response code="500">Server error.</response>
    [Authorize(Policy = nameof(PermissionType.EntitySetupTool))]
    [HttpGet("seed/targets")]
    [ProducesResponseType(typeof(IReadOnlyCollection<DTSeedTarget>), 200)]
    [ProducesResponseType(typeof(IApiErrorResult), 500)]
    public async Task<IActionResult> GetSeedTargetsAsync(
        [FromServices] IMediator mediator,
        CancellationToken cancellationToken)
    {
        ServiceResult<IReadOnlyCollection<DTSeedTarget>> response = await mediator.Send(new GetSeedTargetsRequest(), cancellationToken);

        return response.ToActionResult();
    }

    /// <summary>
    /// Dev tool: initiate seeding the selected parts of the database with their initial reference/catalogue values.
    /// The seed runs asynchronously. Poll <c>GET /system/status?processId=X</c> for progress.
    /// </summary>
    /// <remarks>
    /// The values come entirely from the backend's static seed-data classes; the body only chooses which of them are applied. 
    /// Prerequisites need not be listed — the selection is expanded over them before the job is queued. 
    /// The seed is idempotent (upsert), so it is safe to re-run.
    /// </remarks>
    /// <response code="201">Seed job accepted. Returns the processId.</response>
    /// <response code="400">No targets were selected, or one of them is not a known target.</response>
    /// <response code="500">Server error.</response>
    [Authorize(Policy = nameof(PermissionType.EntitySetupTool))]
    [HttpPost("seed")]
    [ProducesResponseType(typeof(int), 201)]
    [ProducesResponseType(typeof(IApiErrorResult), 400)]
    [ProducesResponseType(typeof(IApiErrorResult), 500)]
    public async Task<IActionResult> InitiateDatabaseSeedAsync(
        [FromServices] IMediator mediator,
        [FromBody] InitiateDatabaseSeedDto request,
        CancellationToken cancellationToken)
    {
        ServiceResult<int> response = await mediator.Send(new InitiateDatabaseSeedRequest(request.Targets), cancellationToken);

        return response.ToActionResult();
    }
}
