using AscendantEmpire.Api.Utilities;
using AscendantEmpire.Application.Common;
using AscendantEmpire.Application.Common.Messaging;
using AscendantEmpire.Application.Features.Technologies.Classes;
using AscendantEmpire.Application.Features.Technologies.Handlers;
using AscendantEmpire.BackendContracts.Technologies;
using AscendantEmpire.BackendContracts.Technologies.GetExperienceTypes;
using AscendantEmpire.BackendContracts.Technologies.GetPlayerResearch;
using AscendantEmpire.BackendContracts.Technologies.GetTechnologies;
using AscendantEmpire.Domain.Enums.Authentication;
using AscendantEmpire.FrontendContracts;
using AscendantEmpire.FrontendContracts.Technologies.GetExperienceTypes;
using AscendantEmpire.FrontendContracts.Technologies.GetPlayerResearch;
using AscendantEmpire.FrontendContracts.Technologies.GetTechnologies;

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

using static AscendantEmpire.Application.Common.ServiceResultBase;

namespace AscendantEmpire.Api.Controllers;

/// <summary>
/// The technology catalogue and a Player's progress through it.
/// </summary>
/// <remarks>
/// <para>
/// One controller owns <c>/technology</c>, so every technology endpoint belongs here however long the file becomes — including the experience type
/// catalogue, which exists only to feed technologies and so sits as a nested resource rather than claiming a prefix of its own.
/// </para>
/// <para>
/// The catalogue endpoints are player-independent and take nothing from the caller. The per-Player research endpoints read the game and player from the
/// caller's game token, never from the body or query string, so a client cannot ask about somebody else's research.
/// </para>
/// </remarks>
[ApiController]
[Route("[controller]")]
public class TechnologyController : ControllerBase
{
    #region Catalogue

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
                    AssetIconPath = cost.AssetIconPath,
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

    #endregion
    #region Catalogue: experience types

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

    #endregion
    #region Per-player research

    /// <summary>
    /// Get the calling Player's research position: their current project, their banked points, and every technology they could research now.
    /// </summary>
    /// <remarks>
    /// Only technologies at Available or Unlocked come back. Locked is excluded because its prerequisites are unmet and Discovered because there is nothing
    /// left to research, so the list is exactly what the Player can choose between. The requirement figures are this Player's own, not the catalogue's, and
    /// an unused bucket is null rather than zero.
    /// </remarks>
    /// <response code="200">The Player's research position</response>
    /// <response code="403">No game entered — call /auth/enter-game first</response>
    /// <response code="404">The Player has no research state, or the Game has no campaign turn queue</response>
    /// <response code="500">Returns an error response with an array of errors in the event of an exception or other server error</response>
    [HttpGet("research")]
    [ProducesResponseType(typeof(IPlayerResearchOverview), 200)]
    [ProducesResponseType(403)]
    [ProducesResponseType(404)]
    [ProducesResponseType(typeof(IApiErrorResult), 500)]
    public async Task<IActionResult> GetPlayerResearchAsync([FromServices] IMediator mediator, CancellationToken cancelToken)
    {
        if (!this.TryGetGameContext(out int gameId, out int playerId, out IActionResult? contextError))
        {
            return contextError!;
        }

        ServiceResult<PlayerResearchOverview> response = await mediator.Send(new GetPlayerResearchRequest(gameId, playerId), cancelToken);

        return response.ToActionResult(contract => new DTPlayerResearchOverview
        {
            CurrentTechnologyId = contract.CurrentTechnologyId,
            CurrentTechnologyDisplayName = contract.CurrentTechnologyDisplayName,
            NextTechnologyId = contract.NextTechnologyId,
            NextTechnologyDisplayName = contract.NextTechnologyDisplayName,
            UnspentDirectedPoints = contract.UnspentDirectedPoints,
            UnspentDiffusionPoints = contract.UnspentDiffusionPoints,
            CurrentRoundNumber = contract.CurrentRoundNumber,
            Researchable = [.. contract.Researchable.Select(technology => new DTResearchableTechnology
            {
                TechnologyId = technology.TechnologyId,
                Code = technology.Code,
                DisplayName = technology.DisplayName,
                Description = technology.Description,
                IconPath = technology.IconPath,
                Era = technology.Era,
                Status = technology.Status,
                DirectedRequired = technology.DirectedRequired,
                ExperienceRequired = technology.ExperienceRequired,
                DiffusionRequired = technology.DiffusionRequired,
                DirectedPoints = technology.DirectedPoints,
                ExperiencePoints = technology.ExperiencePoints,
                DiffusionPoints = technology.DiffusionPoints,
                AcquisitionCosts = [.. technology.AcquisitionCosts.Select(cost => new DTCatalogueAcquisitionCost
                {
                    AssetId = cost.AssetId,
                    AssetCode = cost.AssetCode,
                    AssetDisplayName = cost.AssetDisplayName,
                    AssetIconPath = cost.AssetIconPath,
                    Quantity = cost.Quantity
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
                })],
                IsCurrentProject = technology.IsCurrentProject
            })]
        });
    }

    /// <summary>
    /// Point the calling Player's directed research at a technology, or clear it by sending no technology id.
    /// </summary>
    /// <remarks>
    /// Choosing an Available technology settles its acquisition cost and unlocks it in the same call, because directed research cannot begin until the cost
    /// is paid and the catalogue authors none. A technology that does carry a cost is refused: nothing spends from a Player's Asset banks yet, so the
    /// cost could only be waived rather than paid.
    /// </remarks>
    /// <response code="200">The research project was set or cleared</response>
    /// <response code="400">The technology is Locked, already Discovered, or carries an acquisition cost that cannot be paid</response>
    /// <response code="403">No game entered — call /auth/enter-game first</response>
    /// <response code="404">The Player has no row for that technology, or no research state</response>
    /// <response code="500">Returns an error response with an array of errors in the event of an exception or other server error</response>
    [HttpPut("research-project")]
    [ProducesResponseType(200)]
    [ProducesResponseType(400)]
    [ProducesResponseType(403)]
    [ProducesResponseType(404)]
    [ProducesResponseType(typeof(IApiErrorResult), 500)]
    public async Task<IActionResult> SetPlayerResearchProjectAsync(
        [FromServices] IMediator mediator,
        [FromBody] SetResearchProjectDto project,
        CancellationToken cancelToken)
    {
        if (!this.TryGetGameContext(out int gameId, out int playerId, out IActionResult? contextError))
        {
            return contextError!;
        }

        ServiceResultBase response = await mediator.Send(new SetResearchProjectRequest(gameId, playerId, project.TechnologyId), cancelToken);

        return response.ToActionResult();
    }
    #endregion
}
