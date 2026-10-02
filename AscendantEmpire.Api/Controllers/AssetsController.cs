using AscendantEmpire.Api.Utilities;
using AscendantEmpire.Application.Common;
using AscendantEmpire.Application.Common.Messaging;
using AscendantEmpire.Application.Features.Assets.Classes;
using AscendantEmpire.Application.Features.Assets.Handlers;
using AscendantEmpire.BackendContracts.Assets;
using AscendantEmpire.Domain.Enums.Authentication;
using AscendantEmpire.FrontendContracts;
using AscendantEmpire.FrontendContracts.Assets.GetAssets;

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

using static AscendantEmpire.Application.Common.ServiceResultBase;

namespace AscendantEmpire.Api.Controllers;

[ApiController]
[Route("[controller]")]
public class AssetsController : ControllerBase
{
    /// <summary>
    /// Upsert asset meta-categories and their child categories.
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
    public async Task<IActionResult> UpsertAssetCategoriesAsync(
        [FromServices] IMediator mediator,
        [FromBody] IReadOnlyCollection<UpsertAssetMetaCategoryDto> metaCategories)
    {
        ServiceResultBase response = await mediator.Send(new UpsertAssetsRequest(metaCategories));

        return response.ToActionResult();
    }

    /// <summary>
    /// Get every Asset, for pickers
    /// </summary>
    /// <response code="200">Every Asset, in meta category order and then by name</response>
    /// <response code="500">Returns an error response with an array of errors in the event of an exception or other server error</response>
    [HttpGet]
    [ProducesResponseType(typeof(IAssetSummaryCollection), 200)]
    [ProducesResponseType(typeof(IApiErrorResult), 500)]
    public async Task<IActionResult> GetAssetsAsync([FromServices] IMediator mediator, CancellationToken cancelToken)
    {
        ServiceResult<IReadOnlyCollection<AssetSummary>> response = await mediator.Send(new GetAssetsRequest(), cancelToken);

        return response.ToActionResult(contract => new DTAssetSummaryCollection
        {
            Assets = [.. contract.Select(asset => new DTAssetSummary
            {
                Id = asset.Id,
                Code = asset.Code,
                DisplayName = asset.DisplayName,
                MetaCategoryDisplayName = asset.MetaCategoryDisplayName
            })]
        });
    }
}
