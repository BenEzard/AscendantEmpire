using AscendantEmpire.Api.Utilities;
using AscendantEmpire.Application.Common;
using AscendantEmpire.Application.Handlers.Maps.MapGallery;
using AscendantEmpire.Application.Handlers.Maps.MapGallery.Classes;
using AscendantEmpire.BackendContracts.MapGallery.SearchCampaignMap;
using AscendantEmpire.BackendContracts.MapGallery.UpdateCampaignMapIndex;
using AscendantEmpire.Domain.Entities.Maps.CampaignMaps;
using AscendantEmpire.FrontendContracts;
using AscendantEmpire.FrontendContracts.MapGallery.GetCampaignMapGalleryIndex;
using AscendantEmpire.FrontendContracts.MapGallery.SearchCampaignMapIndex;

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
        ServiceResult<CampaignMapGalleryIndex> response = await mediator.Send(new GetCampaignMapGalleryIndexRequest(id), cancelToken);

        return response.ToActionResult(contract =>
        {
            return new DTCampaignMapGalleryIndex
            {
                CampaignMapGalleryIndexId = response.SuccessValue.CampaignMapOffsetId,
                MapName = response.SuccessValue.MapName,
                Description = response.SuccessValue.Description,
                Width = response.SuccessValue.Width,
                Height = response.SuccessValue.Height,
                CreationStatus = response.SuccessValue.CreationStatus,
                CreationEngine = response.SuccessValue.CreationEngine,
                PropertiesJson = response.SuccessValue.PropertiesJson,
                Status = response.SuccessValue.Status,
                ErrorMessage = response.SuccessValue.ErrorMessage
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
}
