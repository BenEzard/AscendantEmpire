using AscendantEmpire.Api.Utilities;
using AscendantEmpire.Application.Common.Messaging;
using AscendantEmpire.Application.Features.Structures.Classes;
using AscendantEmpire.Application.Features.Structures.Handlers;
using AscendantEmpire.BackendContracts.Structures.GetStructureDisplayInfo;
using AscendantEmpire.FrontendContracts;
using AscendantEmpire.FrontendContracts.Structures.GetStructureDisplayInfo;

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

using static AscendantEmpire.Application.Common.ServiceResultBase;

namespace AscendantEmpire.Api.Controllers;

[ApiController]
[Route("[controller]")]
public class StructuresController : ControllerBase
{
    /// <summary>
    /// Get the name and map image of every kind of Structure that can stand on the campaign map.
    /// </summary>
    /// <remarks>
    /// The catalogue is global, so no game is read from the token: any signed-in caller gets the same list.
    /// </remarks>
    /// <response code="200">Every campaign-map Structure kind, by id</response>
    /// <response code="500">Returns an error response with an array of errors in the event of an exception or other server error</response>
    [Authorize]
    [HttpGet("display")]
    [ProducesResponseType(typeof(IStructureDisplayInfoCollection), 200)]
    [ProducesResponseType(typeof(IApiErrorResult), 500)]
    public async Task<IActionResult> GetStructureDisplayInfoAsync(
        [FromServices] IMediator mediator,
        CancellationToken cancelToken)
    {
        ServiceResult<IReadOnlyCollection<StructureDisplayInfo>> response = await mediator.Send(new GetStructureDisplayInfoRequest(), cancelToken);

        return response.ToActionResult(contract => new DTStructureDisplayInfoCollection
        {
            StructureDisplayInfo =
            [
                .. (contract ?? []).Select(structure => new DTStructureDisplayInfo
                {
                    StructureId = structure.StructureId,
                    Code = structure.Code,
                    Name = structure.Name,
                    MapImagePath = structure.MapImagePath
                })
            ]
        });
    }
}
