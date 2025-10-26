using AscendantEmpire.Api.Utilities;
using AscendantEmpire.Application.Common;
using AscendantEmpire.Application.Handlers.Maps.MapGallery;
using AscendantEmpire.Application.Handlers.Maps.MapGallery.Classes;
using AscendantEmpire.BackendContracts.MapGallery.GenerateCampaignMap;
using AscendantEmpire.Domain.Entities.Maps.CampaignMaps;
using AscendantEmpire.FrontendContracts;

using MediatR;

using Microsoft.AspNetCore.Mvc;

using static AscendantEmpire.Application.Common.ServiceResultBase;

namespace AscendantEmpire.Api.Controllers;

[ApiController]
[Route("[controller]")]
public class MapGalleryController
{
    /// <summary>
    /// Generate a Campaign Map to the Map Gallery
    /// </summary>
    /// <response code="201">Campaign Map successfully created</response>
    /// <response code="400">Validation error</response>
    /// <response code="500">Returns an error response with an array of errors in the event of an exception or other server error</response>
    [HttpPost("generate-campaign-map")]
    [ProducesResponseType(201)]
    [ProducesResponseType(400)]
    [ProducesResponseType(typeof(IApiErrorResult), 500)]
    public async Task<IActionResult> GenerateCampaignMapAsync(
        [FromServices] IMediator mediator,
        [FromBody] GenerateCampaignMapDto mapDetails)
    {
        ServiceResultBase response = await mediator.Send(new GenerateCampaignMapRequest(
            mapDetails.MapName,
            mapDetails.Description,
            mapDetails.Width,
            mapDetails.Height,
            mapDetails.CreationEngine,
            mapDetails.Parameters
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
}
