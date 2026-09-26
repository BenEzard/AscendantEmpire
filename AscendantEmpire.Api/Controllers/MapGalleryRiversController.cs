using AscendantEmpire.Api.Utilities;
using AscendantEmpire.Application.Common;
using AscendantEmpire.Application.Common.Messaging;
using AscendantEmpire.Application.Features.Maps.MapGallery.Classes;
using AscendantEmpire.Application.Features.Maps.MapGallery.Handlers;
using AscendantEmpire.BackendContracts.MapGallery.UpdateCampaignMapRivers;
using AscendantEmpire.Domain.Enums.Authentication;
using AscendantEmpire.FrontendContracts;
using AscendantEmpire.FrontendContracts.MapGallery.UpdateCampaignMapRivers;

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

using static AscendantEmpire.Application.Common.ServiceResultBase;

namespace AscendantEmpire.Api.Controllers;

/// <summary>
/// The rivers authored on a gallery map's tile edges. Kept out of <c>MapGalleryController</c>, which is already long, but under the same route prefix
/// since every edge belongs to one map.
/// </summary>
/// <remarks>
/// There is no read here: a map's river edges come back on <c>GET /map-gallery/{id}/tiles</c> with the tiles that own them, because that is how they
/// are stored and how the editor edits them.
/// </remarks>
[ApiController]
[Route("map-gallery/{id:int}/rivers")]
public class MapGalleryRiversController : ControllerBase
{
    /// <summary>
    /// Set river edges on a gallery map
    /// </summary>
    /// <remarks>
    /// Each change names the tile that <b>owns</b> the edge and one of that tile's two edges, South or East. A client changing a tile's North or West
    /// edge translates the click to the owning neighbour first, wrapping included, so that every edge on the map has exactly one way to be addressed.
    /// <para>
    /// The batch is applied whole or not at all. The authoring rules judge the river network the batch would produce, so a refusal answers 400 with the
    /// offending edges as its error detail; they are not always the edges the request named.
    /// </para>
    /// </remarks>
    /// <response code="200">The edges were set</response>
    /// <response code="400">The batch would not be a legal river network, or a coordinate is off the map</response>
    /// <response code="404">Campaign Map not found</response>
    /// <response code="500">Returns an error response with an array of errors in the event of an exception or other server error</response>
    [Authorize(Policy = nameof(PermissionType.MapEditor))]
    [HttpPut]
    [ProducesResponseType(typeof(IUpdateCampaignMapRiversResponse), 200)]
    [ProducesResponseType(typeof(IApiErrorResult), 400)]
    [ProducesResponseType(typeof(IApiErrorResult), 404)]
    [ProducesResponseType(typeof(IApiErrorResult), 500)]
    public async Task<IActionResult> UpdateAsync(
        [FromServices] IMediator mediator,
        [FromRoute] int id,
        [FromBody] UpdateCampaignMapRiversDto request,
        CancellationToken cancellationToken)
    {
        ServiceResult<int> response = await mediator.Send(new UpdateCampaignMapRiversRequest(
            CampaignMapGalleryIndexId: id,
            Changes: request.Changes), cancellationToken);

        return response.ToActionResult(edgesUpdated => new DTUpdateCampaignMapRiversResponse
        {
            EdgesUpdated = edgesUpdated
        });
    }
}
