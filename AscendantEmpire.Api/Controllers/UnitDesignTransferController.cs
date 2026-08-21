using AscendantEmpire.Api.Utilities;
using AscendantEmpire.Application.Common;
using AscendantEmpire.Application.Features.UnitComponents.Handlers;
using AscendantEmpire.BackendContracts.UnitDesigns.CatalogTransfer;
using AscendantEmpire.Domain.Enums.Authentication;
using AscendantEmpire.FrontendContracts;

using MediatR;

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

using static AscendantEmpire.Application.Common.ServiceResultBase;

namespace AscendantEmpire.Api.Controllers;

/// <summary>
/// Round-trips the unit-design catalogue between databases as a zip, driven by the dev Entity Setup Tool.
/// </summary>
/// <remarks>
/// Export and import both run asynchronously as background processes rather than completing on the request, so every action here returns a processId and the caller polls <c>GET /system/status?processId=X</c> for progress.
/// </remarks>
[ApiController]
[Route("[controller]")]
public class UnitDesignTransferController : ControllerBase
{
    /// <summary>
    /// Dev design tool: initiate an export of selected parts of the unit-design catalogue.
    /// The export runs asynchronously. Poll <c>GET /system/status?processId=X</c> for progress.
    /// Once complete, download via <c>GET /unit-design-transfer/export/download?processId=X</c>.
    /// </summary>
    /// <remarks>
    /// Gated behind <see cref="PermissionType.SystemPanel"/>, since it reads the whole shared catalogue out to a file.
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
}
