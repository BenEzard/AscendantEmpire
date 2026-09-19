using AscendantEmpire.Api.Utilities;
using AscendantEmpire.Application.Common;
using AscendantEmpire.Application.Common.Messaging;
using AscendantEmpire.Application.Features.Maps.MapResources.Classes;
using AscendantEmpire.Application.Features.Maps.MapResources.Handlers;
using AscendantEmpire.BackendContracts.MapResources.GalleryPlacements;
using AscendantEmpire.Domain.Enums.Authentication;
using AscendantEmpire.FrontendContracts;
using AscendantEmpire.FrontendContracts.MapResources.GalleryPlacements;

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

using static AscendantEmpire.Application.Common.ServiceResultBase;

namespace AscendantEmpire.Api.Controllers;

/// <summary>
/// The MapResources placed on a gallery map's tiles by the map editor. Kept out of <c>MapGalleryController</c>, which is already long, but under the same
/// route prefix since each placement belongs to one map.
/// </summary>
[ApiController]
[Route("map-gallery/{id:int}/map-resources")]
public class MapGalleryResourcesController : ControllerBase
{
    /// <summary>
    /// Get every MapResource placed on a gallery map
    /// </summary>
    /// <response code="200">The map's MapResources</response>
    /// <response code="404">Campaign Map not found</response>
    /// <response code="500">Returns an error response with an array of errors in the event of an exception or other server error</response>
    [HttpGet]
    [ProducesResponseType(typeof(IGalleryMapResourceCollection), 200)]
    [ProducesResponseType(typeof(IApiErrorResult), 404)]
    [ProducesResponseType(typeof(IApiErrorResult), 500)]
    public async Task<IActionResult> GetAsync([FromServices] IMediator mediator, int id, CancellationToken cancelToken)
    {
        ServiceResult<IReadOnlyCollection<GalleryMapResource>> response = await mediator.Send(new GetGalleryMapResourcesRequest(id), cancelToken);

        return response.ToActionResult(placements => new DTGalleryMapResourceCollection
        {
            CampaignMapGalleryIndexId = id,
            MapResources = [.. placements]
        });
    }

    /// <summary>
    /// Place a MapResource on a gallery map tile, in its first free slot
    /// </summary>
    /// <response code="201">The placed MapResource</response>
    /// <response code="400">The type cannot exist on the tile, is already on it, or the tile is full</response>
    /// <response code="404">No such tile or type</response>
    /// <response code="500">Returns an error response with an array of errors in the event of an exception or other server error</response>
    [Authorize(Policy = nameof(PermissionType.MapEditor))]
    [HttpPost]
    [ProducesResponseType(typeof(IGalleryMapResource), 201)]
    [ProducesResponseType(typeof(IApiErrorResult), 400)]
    [ProducesResponseType(typeof(IApiErrorResult), 404)]
    [ProducesResponseType(typeof(IApiErrorResult), 500)]
    public async Task<IActionResult> PlaceAsync([FromServices] IMediator mediator, int id, [FromBody] PlaceGalleryMapResourceDto placement)
    {
        ServiceResult<GalleryMapResource> response = await mediator.Send(new PlaceGalleryMapResourceRequest(id, placement));

        return response.ToActionResult(placed => placed);
    }

    /// <summary>
    /// Set a placed MapResource's starting quantity or Distribution scenario
    /// </summary>
    /// <response code="200">The updated MapResource</response>
    /// <response code="400">A specific that does not apply to the type, or an unknown scenario</response>
    /// <response code="404">That MapResource is not on this map</response>
    /// <response code="500">Returns an error response with an array of errors in the event of an exception or other server error</response>
    [Authorize(Policy = nameof(PermissionType.MapEditor))]
    [HttpPut("{placementId:int}")]
    [ProducesResponseType(typeof(IGalleryMapResource), 200)]
    [ProducesResponseType(typeof(IApiErrorResult), 400)]
    [ProducesResponseType(typeof(IApiErrorResult), 404)]
    [ProducesResponseType(typeof(IApiErrorResult), 500)]
    public async Task<IActionResult> UpdateAsync([FromServices] IMediator mediator, int id, int placementId, [FromBody] UpdateGalleryMapResourceDto specifics)
    {
        ServiceResult<GalleryMapResource> response = await mediator.Send(new UpdateGalleryMapResourceRequest(id, placementId, specifics));

        return response.ToActionResult(updated => updated);
    }

    /// <summary>
    /// Remove a MapResource from a gallery map tile
    /// </summary>
    /// <response code="204">Removed</response>
    /// <response code="404">That MapResource is not on this map</response>
    /// <response code="500">Returns an error response with an array of errors in the event of an exception or other server error</response>
    [Authorize(Policy = nameof(PermissionType.MapEditor))]
    [HttpDelete("{placementId:int}")]
    [ProducesResponseType(204)]
    [ProducesResponseType(typeof(IApiErrorResult), 404)]
    [ProducesResponseType(typeof(IApiErrorResult), 500)]
    public async Task<IActionResult> DeleteAsync([FromServices] IMediator mediator, int id, int placementId)
    {
        ServiceResultBase response = await mediator.Send(new DeleteGalleryMapResourceRequest(id, placementId));

        return response.ToActionResult();
    }
}
