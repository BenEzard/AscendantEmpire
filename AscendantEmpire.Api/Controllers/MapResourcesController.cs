using AscendantEmpire.Api.Utilities;
using AscendantEmpire.Application.Common;
using AscendantEmpire.Application.Common.Messaging;
using AscendantEmpire.Application.Features.Maps.MapResources.Classes;
using AscendantEmpire.Application.Features.Maps.MapResources.Handlers;
using AscendantEmpire.BackendContracts.MapResources.GetMapResourceCatalogue;
using AscendantEmpire.BackendContracts.MapResources.PutMapResourceCatalogue;
using AscendantEmpire.Domain.Enums.Authentication;
using AscendantEmpire.FrontendContracts;
using AscendantEmpire.FrontendContracts.MapResources.GetMapResourceCatalogue;

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

using static AscendantEmpire.Application.Common.ServiceResultBase;

namespace AscendantEmpire.Api.Controllers;

/// <summary>
/// The MapResource catalogue: the categories and types the MapResource Type Manager edits and the map editor places. See
/// <c>docs/design/campaign-map/map-resources.md</c>.
/// </summary>
[ApiController]
[Route("[controller]")]
public class MapResourcesController : ControllerBase
{
    /// <summary>
    /// Get the whole MapResource catalogue, grouped by category
    /// </summary>
    /// <response code="200">Every category, in order, each with its MapResourceTypes in order</response>
    /// <response code="500">Returns an error response with an array of errors in the event of an exception or other server error</response>
    [HttpGet("catalogue")]
    [ProducesResponseType(typeof(IMapResourceCatalogue), 200)]
    [ProducesResponseType(typeof(IApiErrorResult), 500)]
    public async Task<IActionResult> GetCatalogueAsync([FromServices] IMediator mediator, CancellationToken cancelToken)
    {
        ServiceResult<MapResourceCatalogue> response = await mediator.Send(new GetMapResourceCatalogueRequest(), cancelToken);

        return response.ToActionResult(contract => new DTMapResourceCatalogue
        {
            Categories = [.. contract.Categories.Select(category => new DTMapResourceCategory
            {
                Id = category.Id,
                Code = category.Code,
                DisplayName = category.DisplayName,
                SortOrder = category.SortOrder,
                Types = [.. category.Types]
            })]
        });
    }

    /// <summary>
    /// Add or update MapResource categories and types, matched by code
    /// </summary>
    /// <response code="204">The catalogue was saved</response>
    /// <response code="400">Validation error, or a removed Distribution scenario is still in use</response>
    /// <response code="500">Returns an error response with an array of errors in the event of an exception or other server error</response>
    [Authorize(Policy = nameof(PermissionType.MapEditor))]
    [HttpPut("catalogue")]
    [ProducesResponseType(204)]
    [ProducesResponseType(typeof(IApiErrorResult), 400)]
    [ProducesResponseType(typeof(IApiErrorResult), 500)]
    public async Task<IActionResult> PutCatalogueAsync([FromServices] IMediator mediator, [FromBody] PutMapResourceCatalogueDto catalogue)
    {
        ServiceResultBase response = await mediator.Send(new PutMapResourceCatalogueRequest(catalogue));

        return response.ToActionResult();
    }

    /// <summary>
    /// Delete a MapResourceType that no map or game uses
    /// </summary>
    /// <response code="204">The type and everything it owned were deleted</response>
    /// <response code="400">The type is placed on a map or used by a game</response>
    /// <response code="404">There is no type with that code</response>
    /// <response code="500">Returns an error response with an array of errors in the event of an exception or other server error</response>
    [Authorize(Policy = nameof(PermissionType.MapEditor))]
    [HttpDelete("types/{code}")]
    [ProducesResponseType(204)]
    [ProducesResponseType(typeof(IApiErrorResult), 400)]
    [ProducesResponseType(typeof(IApiErrorResult), 404)]
    [ProducesResponseType(typeof(IApiErrorResult), 500)]
    public async Task<IActionResult> DeleteTypeAsync([FromServices] IMediator mediator, string code)
    {
        ServiceResultBase response = await mediator.Send(new DeleteMapResourceTypeRequest(code));

        return response.ToActionResult();
    }

    /// <summary>
    /// Delete an empty MapResource category
    /// </summary>
    /// <response code="204">The category was deleted</response>
    /// <response code="400">The category still has types in it</response>
    /// <response code="404">There is no category with that code</response>
    /// <response code="500">Returns an error response with an array of errors in the event of an exception or other server error</response>
    [Authorize(Policy = nameof(PermissionType.MapEditor))]
    [HttpDelete("categories/{code}")]
    [ProducesResponseType(204)]
    [ProducesResponseType(typeof(IApiErrorResult), 400)]
    [ProducesResponseType(typeof(IApiErrorResult), 404)]
    [ProducesResponseType(typeof(IApiErrorResult), 500)]
    public async Task<IActionResult> DeleteCategoryAsync([FromServices] IMediator mediator, string code)
    {
        ServiceResultBase response = await mediator.Send(new DeleteMapResourceCategoryRequest(code));

        return response.ToActionResult();
    }
}
