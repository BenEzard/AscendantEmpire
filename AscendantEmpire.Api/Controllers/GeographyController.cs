using AscendantEmpire.Api.Utilities;
using AscendantEmpire.Application.Common;
using AscendantEmpire.Application.Handlers.Maps.Geographies;
using AscendantEmpire.Application.Handlers.Maps.Geographies.Classes;
using AscendantEmpire.BackendContracts.Geographies;
using AscendantEmpire.BackendContracts.Geographies.AddCampaignMapGeography;
using AscendantEmpire.Domain.Enums.Maps;
using AscendantEmpire.FrontendContracts;
using AscendantEmpire.FrontendContracts.Geographies.GetCampaignMapGeographies;

using MediatR;

using Microsoft.AspNetCore.Mvc;

using static AscendantEmpire.Application.Common.ServiceResultBase;

namespace AscendantEmpire.Api.Controllers;

[Route("[controller]")]
[ApiController]
public class GeographyController : ControllerBase
{
    /// <summary>
    /// Add Campaign Map Geographies
    /// </summary>
    /// <response code="200">Campaign Map Geographies successfully created</response>
    /// <response code="500">Returns an error response with an array of errors in the event of an exception or other server error</response>
    [HttpPost("campaign-map")]
    [ProducesResponseType(201)]
    [ProducesResponseType(typeof(IApiErrorResult), 500)]
    public async Task<IActionResult> AddCampaignMapGeographiesAsync(
        [FromServices] IMediator mediator,
        [FromBody] IReadOnlyCollection<AddOrUpdateCampaignMapGeographyDto> geographies)
    {
        ServiceResultBase response = await mediator.Send(new AddCampaignMapGeographyRequest(geographies));

        return response.ToActionResult();
    }

    /// <summary>
    /// Upsert existing Campaign Map Geographies
    /// </summary>
    /// <response code="201">Campaign Map Geographies successfully upserted</response>
    /// <response code="400">Validation error</response>
    /// <response code="500">Returns an error response with an array of errors in the event of an exception or other server error</response>
    [HttpPut("campaign-map")]
    [ProducesResponseType(204)]
    [ProducesResponseType(400)]
    [ProducesResponseType(404)]
    [ProducesResponseType(typeof(IApiErrorResult), 500)]
    public async Task<IActionResult> UpsertCampaignMapGeographiesAsync(
        [FromServices] IMediator mediator,
        [FromBody] IReadOnlyCollection<AddOrUpdateCampaignMapGeographyDto> biomes)
    {
        ServiceResultBase response = await mediator.Send(new UpsertCampaignMapGeographiesRequest(biomes));

        return response.ToActionResult();
    }

    /// <summary>
    /// Get Campaign Map Geographies
    /// </summary>
    /// <response code="200">All Campaign Map Geographies</response>
    /// <response code="500">Returns an error response with an array of errors in the event of an exception or other server error</response>
    [HttpGet("campaign-map")]
    [ProducesResponseType(typeof(ICampaignMapGeographyCollection), 200)]
    [ProducesResponseType(typeof(IApiErrorResult), 500)]
    public async Task<IActionResult> GetCampaignMapGeographiesAsync([FromServices] IMediator mediator, CancellationToken cancelToken)
    {
        ServiceResult<IReadOnlyCollection<CampaignMapGeography>> response = await mediator.Send(new GetCampaignMapGeographiesRequest(), cancelToken);

        return response.ToActionResult(Contract =>
        {
            return new DTCampaignMapGeographyCollection
            {
                Geographies = response.SuccessValue.Select(g => new DTCampaignMapGeography
                {
                    CampaignMapGeographyType = (CampaignMapGeographyType)g.CampaignMapGeographyType,
                    RelativeElevation = g.RelativeElevation,
                    Name = g.Name,
                    MovementCost = g.MovementCost,
                    MapEditorIcon = g.MapEditorIcon,
                    IsPaintableOnCampaignMap = g.IsPaintableOnCampaignMap
                }).ToList()
            };
        });
    }
}
