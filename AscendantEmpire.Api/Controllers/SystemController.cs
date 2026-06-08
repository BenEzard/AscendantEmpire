using AscendantEmpire.Api.Utilities;
using AscendantEmpire.Application.Features.SystemMonitoring.Classes;
using AscendantEmpire.Application.Features.SystemMonitoring.Handlers;
using AscendantEmpire.FrontendContracts;

using MediatR;

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
}
