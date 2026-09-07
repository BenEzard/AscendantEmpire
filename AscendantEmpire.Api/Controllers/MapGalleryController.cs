using AscendantEmpire.Api.Utilities;
using AscendantEmpire.Application.Common;
using AscendantEmpire.Application.Common.Messaging;
using AscendantEmpire.Application.Data;
using AscendantEmpire.Application.Options;
using AscendantEmpire.Application.Features.Maps;
using AscendantEmpire.Application.Features.Maps.MapGallery.Classes;
using AscendantEmpire.Application.Features.Maps.MapGallery.Handlers;
using AscendantEmpire.Application.MapGeneration;
using AscendantEmpire.BackendContracts.MapGallery.GetCampaignMapStartPositions;
using AscendantEmpire.BackendContracts.MapGallery.GetCampaignMapTiles;
using AscendantEmpire.BackendContracts.MapGallery.PutCampaignMapStartPositions;
using AscendantEmpire.BackendContracts.MapGallery.SearchCampaignMap;
using AscendantEmpire.BackendContracts.MapGallery.UpdateCampaignMapIndex;
using AscendantEmpire.BackendContracts.MapGallery.UpdateCampaignMapTiles;
using AscendantEmpire.Domain.Enums.Authentication;
using AscendantEmpire.Domain.Enums.Processing;
using AscendantEmpire.Domain.Enums.System;
using AscendantEmpire.FrontendContracts;
using AscendantEmpire.FrontendContracts.MapGallery.GetCampaignMapGalleryIndex;
using AscendantEmpire.FrontendContracts.MapGallery.GetCampaignMapStartPositions;
using AscendantEmpire.FrontendContracts.MapGallery.GetCampaignMapTiles;
using AscendantEmpire.FrontendContracts.MapGallery.PutCampaignMapStartPositions;
using AscendantEmpire.FrontendContracts.MapGallery.SearchCampaignMapIndex;
using AscendantEmpire.FrontendContracts.MapGallery.UpdateCampaignMapTiles;

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Microsoft.Net.Http.Headers;

using static AscendantEmpire.Application.Common.ServiceResultBase;

namespace AscendantEmpire.Api.Controllers;

[ApiController]
[Route("[controller]")]
public class MapGalleryController
{
    /// <summary>
    /// Add Campaign Map to the Map Gallery
    /// </summary>
    /// <response code="201">Campaign Map successfully created</response>
    /// <response code="400">Validation error</response>
    /// <response code="500">Returns an error response with an array of errors in the event of an exception or other server error</response>
    [Authorize(Policy = nameof(PermissionType.MapEditor))]
    [HttpPost("add-campaign-map")]
    [ProducesResponseType(typeof(AddCampaignMapResponse), 201)]
    [ProducesResponseType(400)]
    [ProducesResponseType(typeof(IApiErrorResult), 500)]
    public async Task<IActionResult> AddCampaignMapAsync(
        [FromServices] IMediator mediator,
        [FromBody] AddCampaignMapRequest mapDetails)
    {
        ServiceResult<AddCampaignMapResponse> response = await mediator.Send(new AddCampaignMapRequest(
            MapName: mapDetails.MapName,
            Description: mapDetails.Description,
            Width: mapDetails.Width,
            Height: mapDetails.Height,
            CreationEngine: mapDetails.CreationEngine,
            Parameters: mapDetails.Parameters
        ));

        // Returns 201 with { galleryIndexId, processingId } so the UI can poll generation progress.
        return response.ToActionResult();
    }

    /// <summary>
    /// Update the Campaign Map Index in the Map Gallery
    /// </summary>
    /// <response code="200">Campaign Map successfully updated</response>
    /// <response code="400">Validation error</response>
    /// <response code="500">Returns an error response with an array of errors in the event of an exception or other server error</response>
    [Authorize(Policy = nameof(PermissionType.MapEditor))]
    [HttpPost("{id}/update")]
    [ProducesResponseType(200)]
    [ProducesResponseType(400)]
    [ProducesResponseType(typeof(IApiErrorResult), 500)]
    public async Task<IActionResult> UpdateCampaignMapIndexAsync(
        [FromServices] IMediator mediator,
        int id,
        [FromBody] UpdateCampaignMapIndexRequestDto mapDetails)
    {
        ServiceResultBase response = await mediator.Send(new UpdateCampaignMapIndexRequest(
            Id: id,
            MapName: mapDetails.MapName,
            Description: mapDetails.Description
        ));

        return response.ToActionResult();
    }

    /// <summary>
    /// Get Campaign Map Gallery Index record
    /// </summary>
    /// <response code="200">Campaign Map Gallery Index</response>
    /// <response code="500">Returns an error response with an array of errors in the event of an exception or other server error</response>
    [HttpGet("{id}/campaign-map")]
    [ProducesResponseType(typeof(ICampaignMapGalleryIndex), 200)]
    [ProducesResponseType(typeof(IApiErrorResult), 500)]
    public async Task<IActionResult> GetCampaignMapGalleryIndexAsync(
        [FromServices] IMediator mediator, 
        int id,
        CancellationToken cancelToken)
    {
        ServiceResult<DTCampaignMapGalleryIndex> response = await mediator.Send(new GetCampaignMapGalleryIndexRequest(id), cancelToken);

        return response.ToActionResult(contract =>
        {
            return new BackendContracts.MapGallery.GetCampaignMapGalleryIndex.CampaignMapGalleryIndex
            {
                CampaignMapGalleryIndexId = response.SuccessValue.CampaignMapGalleryIndexId,
                MapName = response.SuccessValue.MapName,
                Description = response.SuccessValue.Description,
                Width = response.SuccessValue.Width,
                Height = response.SuccessValue.Height,
                CampaignMapOffsetId = response.SuccessValue.CampaignMapOffsetId,
                CreationStatus = response.SuccessValue.CreationStatus,
                CreationEngine = response.SuccessValue.CreationEngine,
                PropertiesJson = response.SuccessValue.PropertiesJson,
                Status = response.SuccessValue.Status,
                ErrorMessage = response.SuccessValue.ErrorMessage
            };
        });
    }

    /// <summary>
    /// Load a Campaign Map 
    /// </summary>
    /// <response code="200">Campaign Map data</response>
    /// <response code="404">Campaign Map not found</response>
    /// <response code="500">Returns an error response with an array of errors in the event of an exception or other server error</response>
    [HttpGet("{id}/tiles")]
    [ProducesResponseType(typeof(ICampaignMapTileCollection), 200)]
    [ProducesResponseType(typeof(IApiErrorResult), 404)]
    [ProducesResponseType(typeof(IApiErrorResult), 500)]
    public async Task<IActionResult> GetCampaignMapTilesAsync(
        [FromServices] IMediator mediator,
        int id,
        CancellationToken cancelToken)
    {
        ServiceResult<IReadOnlyCollection<DTCampaignMapTile>> response = await mediator.Send(new GetCampaignMapTilesRequest(id), cancelToken);

        return response.ToActionResult(tiles =>
        {
            // Map DTOs -> backend contract DTOs
            var contractTiles = tiles.Select(t => new CampaignMapTile
            {
                CampaignMapGalleryIndexId = t.CampaignMapGalleryIndexId,
                XCoord = t.XCoord,
                YCoord = t.YCoord,
                BiomeTypeId = t.BiomeTypeId,
                CampaignMapGeographyTypeId = t.CampaignMapGeographyTypeId
            }).ToList();

            return new CampaignMapTileCollection
            {
                CampaignMapGalleryIndexId = id,
                Tiles = contractTiles
            };
        });
    }

    /// <summary>
    /// Load authored start positions for a Campaign Map in the gallery.
    /// </summary>
    /// <response code="200">Campaign Map start positions returned</response>
    /// <response code="404">Campaign Map not found</response>
    /// <response code="500">Returns an error response with an array of errors in the event of an exception or other server error</response>
    [HttpGet("{id}/start-positions")]
    [ProducesResponseType(typeof(ICampaignMapStartPositionCollection), 200)]
    [ProducesResponseType(typeof(IApiErrorResult), 404)]
    [ProducesResponseType(typeof(IApiErrorResult), 500)]
    public async Task<IActionResult> GetCampaignMapStartPositionsAsync(
        [FromServices] IMediator mediator,
        int id,
        CancellationToken cancelToken)
    {
        ServiceResult<IReadOnlyCollection<DTCampaignMapStartPosition>> response = await mediator.Send(new GetCampaignMapStartPositionsRequest(id), cancelToken);

        return response.ToActionResult(startPositions =>
        {
            var contractPositions = startPositions.Select(t => new CampaignMapStartPosition
            {
                Id = t.Id,
                CampaignMapGalleryIndexId = t.CampaignMapGalleryIndexId,
                XCoord = t.XCoord,
                YCoord = t.YCoord,
                DisplayName = t.DisplayName,
                LocationScore = t.LocationScore
            }).ToList();

            return new CampaignMapStartPositionCollection
            {
                CampaignMapGalleryIndexId = id,
                StartPositions = contractPositions
            };
        });
    }

    /// <summary>
    /// Replace authored start positions for a Campaign Map in the gallery.
    /// </summary>
    /// <response code="200">Campaign Map start positions updated</response>
    /// <response code="400">Validation error</response>
    /// <response code="404">Campaign Map not found</response>
    /// <response code="500">Returns an error response with an array of errors in the event of an exception or other server error</response>
    [Authorize(Policy = nameof(PermissionType.MapEditor))]
    [HttpPut("{id:int}/start-positions")]
    [ProducesResponseType(typeof(ICampaignMapStartPositionCollection), 200)]
    [ProducesResponseType(typeof(IApiErrorResult), 400)]
    [ProducesResponseType(typeof(IApiErrorResult), 404)]
    [ProducesResponseType(typeof(IApiErrorResult), 500)]
    public async Task<IActionResult> PutCampaignMapStartPositionsAsync(
        [FromServices] IMediator mediator,
        [FromRoute] int id,
        [FromBody] PutCampaignMapStartPositionsDto request,
        CancellationToken cancellationToken)
    {
        ServiceResult<IReadOnlyCollection<DTCampaignMapStartPosition>> response = await mediator.Send(new PutCampaignMapStartPositionsRequest(
            CampaignMapGalleryIndexId: id,
            StartPositions: [.. request.StartPositions.Select(x => new PutCampaignMapStartPositionItem(
                XCoord: x.XCoord,
                YCoord: x.YCoord,
                DisplayName: x.DisplayName,
                LocationScore: x.LocationScore))]), cancellationToken);

        return response.ToActionResult(startPositions =>
        {
            var contractPositions = startPositions.Select(t => new CampaignMapStartPosition
            {
                Id = t.Id,
                CampaignMapGalleryIndexId = t.CampaignMapGalleryIndexId,
                XCoord = t.XCoord,
                YCoord = t.YCoord,
                DisplayName = t.DisplayName,
                LocationScore = t.LocationScore
            }).ToList();

            return new CampaignMapStartPositionCollection
            {
                CampaignMapGalleryIndexId = id,
                StartPositions = contractPositions
            };
        });
    }

    /// <summary>
    /// Search Campaign Map Gallery Index record
    /// </summary>
    /// <response code="200">Campaign Map Gallery Index</response>
    /// <response code="500">Returns an error response with an array of errors in the event of an exception or other server error</response>
    [HttpGet("search")]
    [ProducesResponseType(typeof(ICampaignMapIndexCollection), 200)]
    [ProducesResponseType(typeof(IApiErrorResult), 500)]
    public async Task<IActionResult> SearchCampaignMapGalleryIndexAsync(
        [FromServices] IMediator mediator,
        CancellationToken cancelToken)
    {
        ServiceResult<IReadOnlyCollection<CampaignMapIndex>> response = await mediator.Send(new SearchCampaignMapGalleryIndexRequest(), cancelToken);

        return response.ToActionResult(contract =>
        {
            return new DTCampaignMapIndexCollection
            {
                Indexes = [.. response.SuccessValue.Select(c => new DTCampaignMapIndex
                {
                    CampaignMapGalleryIndexId = c.CampaignMapGalleryIndexId,
                    MapName = c.MapName,
                    Description = c.Description,
                    Width = c.Width,
                    Height = c.Height
                })]
            };
        });
    }

    /// <summary>
    /// Update campaign map tiles for the specified gallery id.
    /// Applies a biome and/or geography change to the supplied coordinates.
    /// Returns partial success if some tiles were rejected, including per-tile revision details.
    /// </summary>
    /// <response code="200">Tiles successfully updated</response>
    /// <response code="206">Tiles successfully partially updated (with errors)</response>
    /// <response code="400">Validation error</response>
    /// <response code="404">Map gallery not found (or other not-found validation error)</response>
    /// <response code="500">Server error</response>
    [Authorize(Policy = nameof(PermissionType.MapEditor))]
    [HttpPost("{id:int}/update-tiles")]
    [ProducesResponseType(typeof(IUpdateCampaignMapTilesResponse), 200)]
    [ProducesResponseType(typeof(IUpdateCampaignMapTilesResponse), 206)]
    [ProducesResponseType(typeof(IApiErrorResult), 400)]
    [ProducesResponseType(typeof(IApiErrorResult), 404)]
    [ProducesResponseType(typeof(IApiErrorResult), 500)]
    public async Task<IActionResult> UpdateCampaignMapTilesAsync(
        [FromServices] IMediator mediator,
        [FromRoute] int id,
        [FromBody] UpdateCampaignMapTilesDto request,
        CancellationToken cancellationToken)
    {
        // id is the CampaignMapGalleryId
        var response = await mediator.Send(new UpdateCampaignMapTilesRequest(
            CampaignMapGalleryIndexId: id,
            Coordinates: request.Coordinates,
            BiomeType: request.BiomeType,
            CampaignMapGeographyType: request.GeographyType
        ), cancellationToken);

        if (response.HasSucceeded)
        {
            return response.ToActionResult(contract =>
            {
                return new DTUpdateCampaignMapTilesResponse
                {
                    TilesUpdated = response.SuccessValue
                };
            });
        }
        else if (response.HasPartiallySucceeded)
        {
            IReadOnlyCollection<TileUpdateFail> fails = response.ErrorValue ?? [];

            var dto = new DTUpdateCampaignMapTilesResponse
            {
                TilesUpdated = response.SuccessValue,
                Failures = [.. fails.Select(f => new DTTileGroupUpdateFail
                {
                    ErrorMessage = f.ErrorMessage,
                    FailedTiles = [.. f.FailedTiles.Select(ft => new DTTileUpdateFailTile
                    {
                        Coord = new DTCoord
                        {
                            X = ft.Coord.X,
                            Y = ft.Coord.Y
                        },
                        OriginalBiomeType = ft.OriginalBiomeType,
                        OriginalGeographyType = ft.OriginalGeographyType
                    })]
                })]
            };

            return new ObjectResult(dto)
            {
                StatusCode = StatusCodes.Status206PartialContent
            };
        }

        return response.ToActionResult();
    }


    /// <summary>
    /// Initiate an export of the specified Campaign Map Gallery entry.
    /// The export runs asynchronously. Poll <c>GET /system/status?processId=X</c> for progress.
    /// Once complete, download via <c>GET /map-gallery/{id}/export/download?processId=X</c>.
    /// </summary>
    /// <response code="201">Export job accepted. Returns the processId.</response>
    /// <response code="404">Map Gallery entry not found.</response>
    /// <response code="500">Server error.</response>
    [Authorize(Policy = nameof(PermissionType.MapEditor))]
    [HttpPost("{id}/export")]
    [ProducesResponseType(typeof(int), 201)]
    [ProducesResponseType(typeof(IApiErrorResult), 404)]
    [ProducesResponseType(typeof(IApiErrorResult), 500)]
    public async Task<IActionResult> InitiateMapExportAsync(
        [FromServices] IMediator mediator,
        int id,
        CancellationToken cancellationToken)
    {
        ServiceResult<int> response = await mediator.Send(
            new InitiateMapExportRequest(MapGalleryIndexId: id), cancellationToken);

        return response.ToActionResult();
    }

    /// <summary>
    /// Download the completed export zip for the specified Campaign Map Gallery entry.
    /// Returns 404 if the export has not yet completed or the processId is not found.
    /// </summary>
    /// <response code="200">Returns the export zip file.</response>
    /// <response code="404">Export not ready or processId not found.</response>
    /// <response code="500">Server error.</response>
    [Authorize(Policy = nameof(PermissionType.MapEditor))]
    [HttpGet("{id}/export/download")]
    [ProducesResponseType(200)]
    [ProducesResponseType(typeof(IApiErrorResult), 404)]
    [ProducesResponseType(typeof(IApiErrorResult), 500)]
    public async Task<IActionResult> DownloadMapExportAsync(
        [FromServices] IDbContextFactory<AscendantEmpireContext> contextFactory,
        int id,
        [FromQuery] int processId,
        CancellationToken cancellationToken)
    {
        try
        {
            await using AscendantEmpireContext context = await contextFactory.CreateDbContextAsync(cancellationToken);

            var processing = await context.Processing
                .AsNoTracking()
                .FirstOrDefaultAsync(
                    p => p.ProcessingId == processId && p.ProcessType == ProcessType.ExportGalleryMap,
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
                processing.ResultReference,
                FileMode.Open,
                FileAccess.Read,
                FileShare.Read,
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
    /// Import a Campaign Map from a previously exported zip file.
    /// The uploaded zip is staged on disk and the import runs asynchronously.
    /// Poll <c>GET /system/status?processId=X</c> for progress.
    /// </summary>
    /// <response code="201">Import job accepted. Returns the processId.</response>
    /// <response code="400">No file supplied or the file is empty.</response>
    /// <response code="500">Server error.</response>
    [Authorize(Policy = nameof(PermissionType.MapEditor))]
    [HttpPost("import")]
    [RequestSizeLimit(50 * 1024 * 1024)]
    [ProducesResponseType(typeof(int), 201)]
    [ProducesResponseType(typeof(IApiErrorResult), 400)]
    [ProducesResponseType(typeof(IApiErrorResult), 500)]
    public async Task<IActionResult> ImportMapAsync(
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

        ServiceResult<int> response = await mediator.Send(
            new InitiateMapImportRequest(ZipPath: stagedPath), cancellationToken);

        return response.ToActionResult();
    }

    /// <summary>
    /// Stream a rendered preview PNG (geography or biome) for the specified Campaign Map Gallery entry.
    /// The image is served inline so the UI can display it directly.
    /// </summary>
    /// <param name="id">Campaign Map Gallery index id.</param>
    /// <param name="mode">Which preview to stream: <c>biome</c> or <c>geography</c>.</param>
    /// <response code="200">Returns the preview PNG.</response>
    /// <response code="400">The <c>mode</c> query value was missing or not one of biome|geography.</response>
    /// <response code="404">Map Gallery entry not found, the preview has not been rendered, or the file is missing on disk.</response>
    /// <response code="500">Server error.</response>
    [HttpGet("{id:int}/preview")]
    [ProducesResponseType(200)]
    [ProducesResponseType(typeof(IApiErrorResult), 400)]
    [ProducesResponseType(typeof(IApiErrorResult), 404)]
    [ProducesResponseType(typeof(IApiErrorResult), 500)]
    public async Task<IActionResult> GetCampaignMapPreviewAsync(
        [FromServices] IDbContextFactory<AscendantEmpireContext> contextFactory,
        int id,
        [FromQuery] string? mode,
        CancellationToken cancellationToken)
    {
        try
        {
            // Resolve the requested preview mode up front so an invalid value fails fast with a 400.
            // "startpositions" overlays red circles on the biome preview, so it also resolves to the biome path.
            bool isGeography;
            bool isStartPositions;
            if (string.Equals(mode, "geography", StringComparison.OrdinalIgnoreCase))
            {
                isGeography = true;
                isStartPositions = false;
            }
            else if (string.Equals(mode, "biome", StringComparison.OrdinalIgnoreCase))
            {
                isGeography = false;
                isStartPositions = false;
            }
            else if (string.Equals(mode, "startpositions", StringComparison.OrdinalIgnoreCase))
            {
                isGeography = false;
                isStartPositions = true;
            }
            else
            {
                return new ObjectResult(new DTApiErrorResult(["Query parameter 'mode' must be one of 'biome', 'geography' or 'startpositions'."]))
                {
                    StatusCode = StatusCodes.Status400BadRequest
                };
            }

            await using AscendantEmpireContext context = await contextFactory.CreateDbContextAsync(cancellationToken);

            // Only the two preview paths are needed — project them to avoid loading the whole index.
            var paths = await context.CampaignMapGalleryIndex
                .AsNoTracking()
                .Where(x => x.Id == id)
                .Select(x => new { x.GeographyMapPath, x.BiomeMapPath })
                .FirstOrDefaultAsync(cancellationToken);

            if (paths is null)
            {
                return new ObjectResult(new DTApiErrorResult(["Map Gallery entry not found."]))
                {
                    StatusCode = StatusCodes.Status404NotFound
                };
            }

            // The start-positions overlay is drawn on top of the biome preview.
            string? previewPath = isGeography ? paths.GeographyMapPath : paths.BiomeMapPath;

            // A null/empty path means the preview was never rendered (e.g. non-Perlin map or a failed render).
            if (string.IsNullOrEmpty(previewPath))
            {
                return new ObjectResult(new DTApiErrorResult(["The requested preview has not been rendered for this map."]))
                {
                    StatusCode = StatusCodes.Status404NotFound
                };
            }

            if (!System.IO.File.Exists(previewPath))
            {
                return new ObjectResult(new DTApiErrorResult(["Preview image is no longer available on the server."]))
                {
                    StatusCode = StatusCodes.Status404NotFound
                };
            }

            // Start-positions mode is composited on demand from the current authored positions, so it always
            // reflects edits made after the one-time map render. The other two modes stream the stored file.
            if (isStartPositions)
            {
                // Load the current start positions for this map (just the coordinates needed to place circles).
                List<(int X, int Y)> startPositions = await context.CampaignMapStartPositions
                    .AsNoTracking()
                    .Where(x => x.CampaignMapGalleryIndexId == id)
                    .Select(x => new ValueTuple<int, int>(x.XCoord, x.YCoord))
                    .ToListAsync(cancellationToken);

                byte[]? overlay = MapPreviewRenderer.RenderStartPositionOverlay(previewPath, startPositions);

                if (overlay is null)
                {
                    return new ObjectResult(new DTApiErrorResult(["The start-positions preview could not be rendered."]))
                    {
                        StatusCode = StatusCodes.Status404NotFound
                    };
                }

                // The composited image depends on both the base file and the current positions, so fold a hash of
                // the coordinates into the ETag — editing positions then invalidates any cached overlay.
                var overlayFileInfo = new FileInfo(previewPath);
                int positionsHash = 17;
                foreach ((int x, int y) in startPositions)
                {
                    positionsHash = positionsHash * 31 + x;
                    positionsHash = positionsHash * 31 + y;
                }

                var overlayEntityTag = new EntityTagHeaderValue(
                    $"\"{overlayFileInfo.Length:x}-{overlayFileInfo.LastWriteTimeUtc.Ticks:x}-{positionsHash:x}\"");

                return new FileContentResult(overlay, "image/png")
                {
                    LastModified = overlayFileInfo.LastWriteTimeUtc,
                    EntityTag = overlayEntityTag
                };
            }

            // Derive caching validators from the file itself. A re-render reuses the same URL,
            // so we deliberately avoid a fixed max-age (which could serve a stale image) and instead
            // let the browser revalidate: it sends If-None-Match / If-Modified-Since and receives a
            // 304 Not Modified when the preview is unchanged, but always picks up a fresh render.
            var fileInfo = new FileInfo(previewPath);
            DateTimeOffset lastModified = fileInfo.LastWriteTimeUtc;
            var entityTag = new EntityTagHeaderValue($"\"{fileInfo.Length:x}-{fileInfo.LastWriteTimeUtc.Ticks:x}\"");

            Stream stream = new FileStream(
                previewPath,
                FileMode.Open,
                FileAccess.Read,
                FileShare.Read,
                bufferSize: 65536,
                useAsync: true);

            // Served inline (no FileDownloadName) so the browser renders it as an image.
            // LastModified/EntityTag let the framework honour conditional requests with a 304.
            return new FileStreamResult(stream, "image/png")
            {
                LastModified = lastModified,
                EntityTag = entityTag
            };
        }
        catch (Exception)
        {
            return new ObjectResult(new DTApiErrorResult(["An error occurred while retrieving the preview image."]))
            {
                StatusCode = StatusCodes.Status500InternalServerError
            };
        }
    }

    /// <summary>
    /// Publish changes to a Map Gallery to any existing Games and Player Maps.
    /// </summary>
    /// <response code="200">Campaign Maps successfully updated</response>
    /// <response code="400">Validation error</response>
    /// <response code="500">Returns an error response with an array of errors in the event of an exception or other server error</response>
    [Authorize(Policy = nameof(PermissionType.MapEditor))]
    [HttpPost("{id}/publish")]
    [ProducesResponseType(200)]
    [ProducesResponseType(400)]
    [ProducesResponseType(typeof(IApiErrorResult), 500)]
    public async Task<IActionResult> PublishCampaignMapUpdatesAsync(
        [FromServices] IMediator mediator,
        int id)
    {
        ServiceResultBase response = await mediator.Send(new PublishCampaignMapUpdatesRequest(MapGalleryIndex: id));

        return response.ToActionResult();
    }
}
