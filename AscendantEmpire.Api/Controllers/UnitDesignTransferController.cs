using AscendantEmpire.Api.Utilities;
using AscendantEmpire.Application.Common;
using AscendantEmpire.Application.Data;
using AscendantEmpire.Application.Features.UnitComponents.Handlers;
using AscendantEmpire.Application.Options;
using AscendantEmpire.BackendContracts.UnitDesigns.CatalogTransfer;
using AscendantEmpire.Domain.Enums.Authentication;
using AscendantEmpire.Domain.Enums.Processing;
using AscendantEmpire.Domain.Enums.System;
using AscendantEmpire.FrontendContracts;

using MediatR;

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

using static AscendantEmpire.Application.Common.ServiceResultBase;

namespace AscendantEmpire.Api.Controllers;

/// <summary>
/// Round-trips the unit-design catalogue between databases as a zip, driven by the dev Entity Setup Tool.
/// </summary>
/// <remarks>
/// <para>
/// Export and import both run asynchronously as background processes rather than completing on the request, so every action here returns a processId and the caller polls <c>GET /system/status?processId=X</c> for progress.
/// </para>
/// <para>
/// Two architectural styles live here deliberately: the job-initiating actions go through MediatR, while the download reads the process row directly through an <see cref="IDbContextFactory{TContext}"/> and streams a file off disk. That mix is the reason transfer is its own controller rather than part of the catalogue.
/// </para>
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

            // System.IO.File is qualified because ControllerBase.File(...) shadows the type name here.
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
    /// Gated behind <see cref="PermissionType.SystemPanel"/>, since it overwrites the shared catalogue from an uploaded file.
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
