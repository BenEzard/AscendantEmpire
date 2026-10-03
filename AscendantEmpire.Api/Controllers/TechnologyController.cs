using AscendantEmpire.Api.Utilities;
using AscendantEmpire.Application.Common;
using AscendantEmpire.Application.Common.Messaging;
using AscendantEmpire.Application.Features.Technologies.Classes;
using AscendantEmpire.Application.Features.Technologies.Handlers;
using AscendantEmpire.BackendContracts.Technologies;
using AscendantEmpire.BackendContracts.Technologies.GetExperienceTypes;
using AscendantEmpire.BackendContracts.Technologies.GetTechnologies;
using AscendantEmpire.Domain.Enums.Authentication;
using AscendantEmpire.FrontendContracts;
using AscendantEmpire.FrontendContracts.Technologies.GetExperienceTypes;
using AscendantEmpire.FrontendContracts.Technologies.GetTechnologies;

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

using static AscendantEmpire.Application.Common.ServiceResultBase;

namespace AscendantEmpire.Api.Controllers;

/// <summary>
/// The technology catalogue and, in time, a Player's progress through it.
/// </summary>
/// <remarks>
/// <para>
/// One controller owns <c>/technology</c>, so every technology endpoint belongs here however long the file becomes — including the experience type
/// catalogue, which exists only to feed technologies and so sits as a nested resource rather than claiming a prefix of its own.
/// </para>
/// <para>
/// Only the catalogue writes exist so far. The per-Player reads and the research-project selection wait on the handlers behind them, and will be added here
/// under a banner comment rather than in a second controller.
/// </para>
/// </remarks>
[ApiController]
[Route("[controller]")]
public class TechnologyController : ControllerBase
{
    // --- Catalogue: technologies -----------------------------------------------------

    /// <summary>
    /// Get the whole technology catalogue, with each technology's costs, prerequisites, grants and experience sources.
    /// </summary>
    /// <remarks>
    /// Open to any logged-in user rather than gated behind the Entity Setup Tool: the tech tree is part of ordinary play, not just of editing. It is
    /// player-independent while technology customisation is Identical, so a client may hold it for the life of a game. The three requirement figures are the
    /// catalogue's base numbers, not any Player's.
    /// </remarks>
    /// <response code="200">Every technology, in era and then sort order</response>
    /// <response code="500">Returns an error response with an array of errors in the event of an exception or other server error</response>
    [HttpGet]
    [ProducesResponseType(typeof(ITechnologyCatalogue), 200)]
    [ProducesResponseType(typeof(IApiErrorResult), 500)]
    public async Task<IActionResult> GetTechnologiesAsync([FromServices] IMediator mediator, CancellationToken cancelToken)
    {
        ServiceResult<IReadOnlyCollection<CatalogueTechnology>> response = await mediator.Send(new GetTechnologiesRequest(), cancelToken);

        return response.ToActionResult(contract => new DTTechnologyCatalogue
        {
            Technologies = [.. contract.Select(technology => new DTCatalogueTechnology
            {
                Id = technology.Id,
                Code = technology.Code,
                DisplayName = technology.DisplayName,
                Description = technology.Description,
                IconPath = technology.IconPath,
                Era = technology.Era,
                SortOrder = technology.SortOrder,
                TreeColumn = technology.TreeColumn,
                TreeRow = technology.TreeRow,
                DirectedRequired = technology.DirectedRequired,
                ExperienceRequired = technology.ExperienceRequired,
                DiffusionRequired = technology.DiffusionRequired,
                AcquisitionCosts = [.. technology.AcquisitionCosts.Select(cost => new DTCatalogueAcquisitionCost
                {
                    AssetId = cost.AssetId,
                    AssetCode = cost.AssetCode,
                    AssetDisplayName = cost.AssetDisplayName,
                    Quantity = cost.Quantity
                })],
                Prerequisites = [.. technology.Prerequisites.Select(prerequisite => new DTCataloguePrerequisite
                {
                    PrerequisiteTechnologyId = prerequisite.PrerequisiteTechnologyId,
                    PrerequisiteTechnologyCode = prerequisite.PrerequisiteTechnologyCode,
                    PrerequisiteTechnologyDisplayName = prerequisite.PrerequisiteTechnologyDisplayName,
                    PrerequisiteKind = prerequisite.PrerequisiteKind
                })],
                Grants = [.. technology.Grants.Select(grant => new DTCatalogueGrant
                {
                    TargetType = grant.TargetType,
                    TargetId = grant.TargetId,
                    TargetCode = grant.TargetCode,
                    TargetDisplayName = grant.TargetDisplayName,
                    GrantEffect = grant.GrantEffect
                })],
                ExperienceSources = [.. technology.ExperienceSources.Select(source => new DTCatalogueExperienceSource
                {
                    ExperienceTypeId = source.ExperienceTypeId,
                    ExperienceTypeCode = source.ExperienceTypeCode,
                    ExperienceTypeDisplayName = source.ExperienceTypeDisplayName
                })]
            })]
        });
    }

    /// <summary>
    /// Upsert technologies, with their acquisition costs, prerequisites, grants and experience mappings.
    /// Existing records (matched by Code) are updated; new records are inserted.
    /// </summary>
    /// <remarks>
    /// Each technology's four child collections are replaced wholesale rather than merged, so an omitted collection clears it. Prerequisites may name a
    /// technology defined elsewhere in the same request, which is what lets one call carry a whole tree.
    /// </remarks>
    /// <response code="204">Technologies successfully upserted</response>
    /// <response code="400">Validation error — an unresolvable grant target or cost Asset, a prerequisite cycle, a duplicate code, or a technology requiring no research bucket</response>
    /// <response code="500">Returns an error response with an array of errors in the event of an exception or other server error</response>
    [Authorize(Policy = nameof(PermissionType.EntitySetupTool))]
    [HttpPut]
    [ProducesResponseType(204)]
    [ProducesResponseType(400)]
    [ProducesResponseType(typeof(IApiErrorResult), 500)]
    public async Task<IActionResult> UpsertTechnologiesAsync(
        [FromServices] IMediator mediator,
        [FromBody] IReadOnlyCollection<UpsertTechnologyDto> technologies,
        CancellationToken cancelToken)
    {
        ServiceResultBase response = await mediator.Send(new UpsertTechnologiesRequest(technologies), cancelToken);

        return response.ToActionResult();
    }

    // --- Catalogue: experience types --------------------------------------------------

    /// <summary>
    /// Get every kind of experience an action can earn, in display order.
    /// </summary>
    /// <remarks>
    /// Served separately as well as nested under each technology, because the editor needs the whole list to choose from. Open to any logged-in user, for the
    /// same reason the technology catalogue is.
    /// </remarks>
    /// <response code="200">Every experience type, in sort order and then by name</response>
    /// <response code="500">Returns an error response with an array of errors in the event of an exception or other server error</response>
    [HttpGet("experience-types")]
    [ProducesResponseType(typeof(IExperienceTypeCollection), 200)]
    [ProducesResponseType(typeof(IApiErrorResult), 500)]
    public async Task<IActionResult> GetExperienceTypesAsync([FromServices] IMediator mediator, CancellationToken cancelToken)
    {
        ServiceResult<IReadOnlyCollection<ExperienceTypeDetail>> response = await mediator.Send(new GetExperienceTypesRequest(), cancelToken);

        return response.ToActionResult(contract => new DTExperienceTypeCollection
        {
            ExperienceTypes = [.. contract.Select(experienceType => new DTExperienceTypeDetail
            {
                Id = experienceType.Id,
                Code = experienceType.Code,
                DisplayName = experienceType.DisplayName,
                Description = experienceType.Description,
                IconPath = experienceType.IconPath,
                SortOrder = experienceType.SortOrder
            })]
        });
    }

    /// <summary>
    /// Upsert the kinds of experience an action can earn.
    /// Existing records (matched by Code) are updated; new records are inserted.
    /// </summary>
    /// <remarks>
    /// Must be populated before any technology naming one of these codes is upserted, which is also why the technology seed step depends on the experience
    /// type step.
    /// </remarks>
    /// <response code="204">Experience types successfully upserted</response>
    /// <response code="400">Validation error — a blank or duplicate code, or a duplicate sort order</response>
    /// <response code="500">Returns an error response with an array of errors in the event of an exception or other server error</response>
    [Authorize(Policy = nameof(PermissionType.EntitySetupTool))]
    [HttpPut("experience-types")]
    [ProducesResponseType(204)]
    [ProducesResponseType(400)]
    [ProducesResponseType(typeof(IApiErrorResult), 500)]
    public async Task<IActionResult> UpsertExperienceTypesAsync(
        [FromServices] IMediator mediator,
        [FromBody] IReadOnlyCollection<UpsertExperienceTypeDto> experienceTypes,
        CancellationToken cancelToken)
    {
        ServiceResultBase response = await mediator.Send(new UpsertExperienceTypesRequest(experienceTypes), cancelToken);

        return response.ToActionResult();
    }
}
