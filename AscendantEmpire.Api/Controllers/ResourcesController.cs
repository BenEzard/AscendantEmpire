using AscendantEmpire.Api.Utilities;
using AscendantEmpire.Application.Common;
using AscendantEmpire.Application.Common.Messaging;
using AscendantEmpire.Application.Features.Resources.Classes;
using AscendantEmpire.Application.Features.Resources.Handlers;
using AscendantEmpire.BackendContracts.Resources;
using AscendantEmpire.Domain.Enums.Authentication;
using AscendantEmpire.FrontendContracts;
using AscendantEmpire.FrontendContracts.Resources.GetResources;

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

using static AscendantEmpire.Application.Common.ServiceResultBase;

namespace AscendantEmpire.Api.Controllers;

[ApiController]
[Route("[controller]")]
public class ResourcesController : ControllerBase
{
    /// <summary>
    /// Upsert resource meta-categories and their child categories.
    /// Existing records (matched by Code) are updated; new records are inserted.
    /// </summary>
    /// <response code="204">Meta-categories and categories successfully upserted</response>
    /// <response code="400">Validation error</response>
    /// <response code="500">Returns an error response with an array of errors in the event of an exception or other server error</response>
    [Authorize(Policy = nameof(PermissionType.EntitySetupTool))]
    [HttpPut]
    [ProducesResponseType(204)]
    [ProducesResponseType(400)]
    [ProducesResponseType(typeof(IApiErrorResult), 500)]
    public async Task<IActionResult> UpsertResourceCategoriesAsync(
        [FromServices] IMediator mediator,
        [FromBody] IReadOnlyCollection<UpsertResourceMetaCategoryDto> metaCategories)
    {
        ServiceResultBase response = await mediator.Send(new UpsertResourcesRequest(metaCategories));

        return response.ToActionResult();
    }

    /// <summary>
    /// Get every Resource, for pickers
    /// </summary>
    /// <response code="200">Every Resource, in meta category order and then by name</response>
    /// <response code="500">Returns an error response with an array of errors in the event of an exception or other server error</response>
    [HttpGet]
    [ProducesResponseType(typeof(IResourceSummaryCollection), 200)]
    [ProducesResponseType(typeof(IApiErrorResult), 500)]
    public async Task<IActionResult> GetResourcesAsync([FromServices] IMediator mediator, CancellationToken cancelToken)
    {
        ServiceResult<IReadOnlyCollection<ResourceSummary>> response = await mediator.Send(new GetResourcesRequest(), cancelToken);

        return response.ToActionResult(contract => new DTResourceSummaryCollection
        {
            Resources = [.. contract.Select(resource => new DTResourceSummary
            {
                Id = resource.Id,
                Code = resource.Code,
                DisplayName = resource.DisplayName,
                MetaCategoryDisplayName = resource.MetaCategoryDisplayName
            })]
        });
    }
}
