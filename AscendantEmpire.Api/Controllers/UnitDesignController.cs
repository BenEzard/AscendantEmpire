using AscendantEmpire.Api.Utilities;
using AscendantEmpire.Application.Features.UnitComponents.Classes;
using AscendantEmpire.Application.Features.UnitComponents.Handlers;
using AscendantEmpire.BackendContracts.UnitDesigns.BulkUpsertUnitComponents;
using AscendantEmpire.FrontendContracts;
using AscendantEmpire.FrontendContracts.UnitDesigns.BulkUpsertUnitComponents;

using MediatR;

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
    /// Writes the full component graph in one transaction. Components whose Code is new are inserted;
    /// components whose Code already exists are updated in place. The response reports how many were
    /// added vs updated. A component's type cannot be changed by an update.
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
}
