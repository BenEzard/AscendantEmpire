using AscendantEmpire.Api.Utilities;
using AscendantEmpire.Application.Common;
using AscendantEmpire.Application.Handlers.Maps;
using AscendantEmpire.Application.Handlers.Maps.MapGallery;
using AscendantEmpire.Application.Handlers.Maps.MapGallery.Classes;
using AscendantEmpire.BackendContracts.MapGallery.GetCampaignMapTiles;
using AscendantEmpire.BackendContracts.MapGallery.SearchCampaignMap;
using AscendantEmpire.BackendContracts.MapGallery.UpdateCampaignMapIndex;
using AscendantEmpire.BackendContracts.MapGallery.UpdateCampaignMapTiles;
using AscendantEmpire.FrontendContracts;
using AscendantEmpire.FrontendContracts.MapGallery.GetCampaignMapGalleryIndex;
using AscendantEmpire.FrontendContracts.MapGallery.GetCampaignMapTiles;
using AscendantEmpire.FrontendContracts.MapGallery.SearchCampaignMapIndex;
using AscendantEmpire.FrontendContracts.MapGallery.UpdateCampaignMapTiles;

using MediatR;

using Microsoft.AspNetCore.Mvc;

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
    [HttpPost("add-campaign-map")]
    [ProducesResponseType(201)]
    [ProducesResponseType(400)]
    [ProducesResponseType(typeof(IApiErrorResult), 500)]
    public async Task<IActionResult> AddCampaignMapAsync(
        [FromServices] IMediator mediator,
        [FromBody] AddCampaignMapRequest mapDetails)
    {
        ServiceResultBase response = await mediator.Send(new AddCampaignMapRequest(
            MapName: mapDetails.MapName,
            Description: mapDetails.Description,
            Width: mapDetails.Width,
            Height: mapDetails.Height,
            CreationEngine: mapDetails.CreationEngine,
            Parameters: mapDetails.Parameters
        ));

        return response.ToActionResult();
    }

    /// <summary>
    /// A temporary endpoint to cause the generation of the next Campaign Map.
    /// </summary>
    /// <response code="201">Campaign Map successfully created</response>
    /// <response code="400">Validation error</response>
    /// <response code="500">Returns an error response with an array of errors in the event of an exception or other server error</response>
    [HttpPost("temporary-dev-only/prod-map-creation")]
    [ProducesResponseType(201)]
    [ProducesResponseType(400)]
    [ProducesResponseType(typeof(IApiErrorResult), 500)]
    public async Task<IActionResult> ProdCampaignMapCreationAsync(
        [FromServices] IMediator mediator)
    {
        ServiceResultBase response = await mediator.Send(new CreateCampaignMapRequest());

        return response.ToActionResult();
    }

    /// <summary>
    /// Update the Campaign Map Index in the Map Gallery
    /// </summary>
    /// <response code="200">Campaign Map successfully updated</response>
    /// <response code="400">Validation error</response>
    /// <response code="500">Returns an error response with an array of errors in the event of an exception or other server error</response>
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
            Description: mapDetails.Description,
            Width: mapDetails.Width,
            Height: mapDetails.Height,
            CreationEngine: mapDetails.CreationEngine,
            Parameters: mapDetails.Parameters
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

}
