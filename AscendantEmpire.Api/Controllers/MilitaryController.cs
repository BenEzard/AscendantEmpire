using AscendantEmpire.Api.Utilities;
using AscendantEmpire.Application.Common;
using AscendantEmpire.Application.Features.Military.Handlers;
using AscendantEmpire.BackendContracts.Military.GetOfficerRanks;
using AscendantEmpire.BackendContracts.Military.GetOrganisationStructure;
using AscendantEmpire.BackendContracts.Military.GetOrganisationTags;
using AscendantEmpire.BackendContracts.Military.SaveOfficerRanks;
using AscendantEmpire.BackendContracts.Military.SaveOrganisationStructure;
using AscendantEmpire.FrontendContracts;

using MediatR;

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

using static AscendantEmpire.Application.Common.ServiceResultBase;

namespace AscendantEmpire.Api.Controllers;

/// <summary>
/// Per-player military organisation configuration for the Military Advisor: officer rank ladders and
/// organisation structures. All endpoints are game-scoped — the game and player are read from the caller's
/// game token, never from the request.
/// </summary>
[ApiController]
[Route("[controller]")]
public class MilitaryController : ControllerBase
{
    /// <summary>
    /// Get the calling player's officer rank ladders across all domains (the Officer Class page).
    /// </summary>
    /// <response code="200">The player's rank ladders</response>
    /// <response code="403">No game entered (the token has no game context)</response>
    /// <response code="500">Returns an error response with an array of errors in the event of an exception or other server error</response>
    [Authorize]
    [HttpGet("officer-ranks")]
    [ProducesResponseType(typeof(GetOfficerRanksResult), 200)]
    [ProducesResponseType(typeof(IApiErrorResult), 403)]
    [ProducesResponseType(typeof(IApiErrorResult), 500)]
    public async Task<IActionResult> GetOfficerRanksAsync(
        [FromServices] IMediator mediator,
        CancellationToken cancelToken)
    {
        if (!this.TryGetGameContext(out int _, out int playerId, out IActionResult? contextError))
        {
            return contextError!;
        }

        ServiceResult<GetOfficerRanksResult> response = await mediator.Send(new GetOfficerRanksRequest(playerId), cancelToken);

        return response.ToActionResult();
    }

    /// <summary>
    /// Bulk-save the calling player's officer rank ladders. Every domain present in the body is replaced with
    /// the rows supplied for it.
    /// </summary>
    /// <response code="204">The ranks were saved</response>
    /// <response code="400">Validation error</response>
    /// <response code="403">No game entered (the token has no game context)</response>
    /// <response code="500">Returns an error response with an array of errors in the event of an exception or other server error</response>
    [Authorize]
    [HttpPut("officer-ranks")]
    [ProducesResponseType(204)]
    [ProducesResponseType(typeof(IApiErrorResult), 400)]
    [ProducesResponseType(typeof(IApiErrorResult), 403)]
    [ProducesResponseType(typeof(IApiErrorResult), 500)]
    public async Task<IActionResult> SaveOfficerRanksAsync(
        [FromServices] IMediator mediator,
        [FromBody] IReadOnlyCollection<SaveOfficerRankDto> request,
        CancellationToken cancelToken)
    {
        if (!this.TryGetGameContext(out int _, out int playerId, out IActionResult? contextError))
        {
            return contextError!;
        }

        ServiceResultBase response = await mediator.Send(new SaveOfficerRanksRequest(playerId, request), cancelToken);

        return response.ToActionResult();
    }

    /// <summary>
    /// Get the MilitaryOrganisationalGroup tags — the Organisation page's searchable combo box feed.
    /// </summary>
    /// <response code="200">The organisation tags</response>
    /// <response code="403">No game entered (the token has no game context)</response>
    /// <response code="500">Returns an error response with an array of errors in the event of an exception or other server error</response>
    [Authorize]
    [HttpGet("organisation-tags")]
    [ProducesResponseType(typeof(GetOrganisationTagsResult), 200)]
    [ProducesResponseType(typeof(IApiErrorResult), 403)]
    [ProducesResponseType(typeof(IApiErrorResult), 500)]
    public async Task<IActionResult> GetOrganisationTagsAsync(
        [FromServices] IMediator mediator,
        CancellationToken cancelToken)
    {
        if (!this.TryGetGameContext(out int _, out int _, out IActionResult? contextError))
        {
            return contextError!;
        }

        ServiceResult<GetOrganisationTagsResult> response = await mediator.Send(new GetOrganisationTagsRequest(), cancelToken);

        return response.ToActionResult();
    }

    /// <summary>
    /// Get the calling player's organisation structure for a tag. Returns the code default (unsaved) when the
    /// player has not yet customised one.
    /// </summary>
    /// <response code="200">The organisation structure (saved or default)</response>
    /// <response code="403">No game entered (the token has no game context)</response>
    /// <response code="404">No MilitaryOrganisationalGroup tag exists with the given id</response>
    /// <response code="500">Returns an error response with an array of errors in the event of an exception or other server error</response>
    [Authorize]
    [HttpGet("organisation-structures/{tagId:int}")]
    [ProducesResponseType(typeof(OrganisationStructureView), 200)]
    [ProducesResponseType(typeof(IApiErrorResult), 403)]
    [ProducesResponseType(typeof(IApiErrorResult), 404)]
    [ProducesResponseType(typeof(IApiErrorResult), 500)]
    public async Task<IActionResult> GetOrganisationStructureAsync(
        [FromServices] IMediator mediator,
        int tagId,
        CancellationToken cancelToken)
    {
        if (!this.TryGetGameContext(out int _, out int playerId, out IActionResult? contextError))
        {
            return contextError!;
        }

        ServiceResult<OrganisationStructureView> response =
            await mediator.Send(new GetOrganisationStructureRequest(playerId, tagId), cancelToken);

        return response.ToActionResult();
    }

    /// <summary>
    /// Save (create or replace) the calling player's organisation structure for a tag.
    /// </summary>
    /// <response code="204">The structure was saved</response>
    /// <response code="400">Validation error</response>
    /// <response code="403">No game entered (the token has no game context)</response>
    /// <response code="404">No MilitaryOrganisationalGroup tag exists with the given id</response>
    /// <response code="500">Returns an error response with an array of errors in the event of an exception or other server error</response>
    [Authorize]
    [HttpPut("organisation-structures/{tagId:int}")]
    [ProducesResponseType(204)]
    [ProducesResponseType(typeof(IApiErrorResult), 400)]
    [ProducesResponseType(typeof(IApiErrorResult), 403)]
    [ProducesResponseType(typeof(IApiErrorResult), 404)]
    [ProducesResponseType(typeof(IApiErrorResult), 500)]
    public async Task<IActionResult> SaveOrganisationStructureAsync(
        [FromServices] IMediator mediator,
        int tagId,
        [FromBody] SaveOrganisationStructureDto request,
        CancellationToken cancelToken)
    {
        if (!this.TryGetGameContext(out int _, out int playerId, out IActionResult? contextError))
        {
            return contextError!;
        }

        ServiceResultBase response =
            await mediator.Send(new SaveOrganisationStructureRequest(playerId, tagId, request.Tiers), cancelToken);

        return response.ToActionResult();
    }
}
