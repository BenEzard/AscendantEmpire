using AscendantEmpire.Api.Utilities;
using AscendantEmpire.Application.Common;
using AscendantEmpire.Application.Features.Assets.Handlers;
using AscendantEmpire.BackendContracts.Assets;
using AscendantEmpire.FrontendContracts;

using MediatR;

using Microsoft.AspNetCore.Mvc;

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
    [HttpPut]
    [ProducesResponseType(204)]
    [ProducesResponseType(400)]
    [ProducesResponseType(typeof(IApiErrorResult), 500)]
    public async Task<IActionResult> UpsertResourceCategoriesAsync(
        [FromServices] IMediator mediator,
        [FromBody] IReadOnlyCollection<UpsertResourceMetaCategoryDto> metaCategories)
    {
        ServiceResultBase response = await mediator.Send(new UpsertResourceCategoriesRequest(metaCategories));

        return response.ToActionResult();
    }
}
