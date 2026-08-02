using AscendantEmpire.Api.Utilities;
using AscendantEmpire.Application.Features.DatabaseSeeding.Handlers;
using AscendantEmpire.Application.Features.SystemMonitoring.Classes;
using AscendantEmpire.Application.Features.SystemMonitoring.Handlers;
using AscendantEmpire.Domain.Enums.Authentication;
using AscendantEmpire.FrontendContracts;

using MediatR;

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

using static AscendantEmpire.Application.Common.ServiceResultBase;

namespace AscendantEmpire.Api.Controllers;

[ApiController]
[Route("[controller]")]
public class SystemController
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
    /// Dev tool: initiate seeding the database with its initial reference/catalogue values.
    /// The seed runs asynchronously. Poll <c>GET /system/status?processId=X</c> for progress.
    /// </summary>
    /// <remarks>
    /// Values come entirely from the backend's static seed-data classes; the request takes no body. The seed
    /// is idempotent (upsert), so it is safe to re-run.
    /// </remarks>
    /// <response code="201">Seed job accepted. Returns the processId.</response>
    /// <response code="500">Server error.</response>
    [Authorize(Policy = nameof(PermissionType.EntitySetupTool))]
    [HttpPost("seed")]
    [ProducesResponseType(typeof(int), 201)]
    [ProducesResponseType(typeof(IApiErrorResult), 500)]
    public async Task<IActionResult> InitiateDatabaseSeedAsync(
        [FromServices] IMediator mediator,
        CancellationToken cancellationToken)
    {
        ServiceResult<int> response = await mediator.Send(new InitiateDatabaseSeedRequest(), cancellationToken);

        return response.ToActionResult();
    }
}
