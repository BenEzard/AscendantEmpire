using AcendantEmpire.BackendContracts.Biomes;

using AscendantEmpire.Api.Utilities;
using AscendantEmpire.Application.Common;
using AscendantEmpire.Application.Handlers.Maps.Biomes;
using AscendantEmpire.Application.Handlers.Maps.Biomes.Classes;
using AscendantEmpire.BackendContracts.Biomes;
using AscendantEmpire.FrontendContracts;
using AscendantEmpire.FrontendContracts.Biomes.GetCampaignMapBiomes;

using MediatR;

using Microsoft.AspNetCore.Mvc;

using static AscendantEmpire.Application.Common.ServiceResultBase;

namespace AscendantEmpire.Api.Controllers
{
    [ApiController]
    [Route("[controller]")]
    public class BiomesController : ControllerBase
    {
        /// <summary>
        /// Add Campaign Map Biomes
        /// </summary>
        /// <response code="201">Campaign Map Biomes successfully created</response>
        /// <response code="400">Validation error</response>
        /// <response code="500">Returns an error response with an array of errors in the event of an exception or other server error</response>
        [HttpPost("campaign-map")]
        [ProducesResponseType(201)]
        [ProducesResponseType(400)]
        [ProducesResponseType(typeof(IApiErrorResult), 500)]
        public async Task<IActionResult> AddCampaignMapBiomesAsync(
            [FromServices] IMediator mediator,
            [FromBody] IReadOnlyCollection<AddOrUpdateCampaignMapBiomeDto> biomes)
        {
            ServiceResultBase response = await mediator.Send(new AddCampaignMapBiomesRequest(biomes));

            return response.ToActionResult();
        }

        /// <summary>
        /// Upsert existing Campaign Map Biomes
        /// </summary>
        /// <response code="201">Campaign Map Biomes successfully upserted</response>
        /// <response code="400">Validation error</response>
        /// <response code="500">Returns an error response with an array of errors in the event of an exception or other server error</response>
        [HttpPut("campaign-map")]
        [ProducesResponseType(204)]
        [ProducesResponseType(400)]
        [ProducesResponseType(404)]
        [ProducesResponseType(typeof(IApiErrorResult), 500)]
        public async Task<IActionResult> UpsertCampaignMapBiomesAsync(
            [FromServices] IMediator mediator,
            [FromBody] IReadOnlyCollection<AddOrUpdateCampaignMapBiomeDto> biomes)
        {
            ServiceResultBase response = await mediator.Send(new UpsertCampaignMapBiomesRequest(biomes));

            return response.ToActionResult();
        }

        /// <summary>
        /// Get Campaign Map Biomes
        /// </summary>
        /// <response code="200">All Campaign Map Biomes</response>
        /// <response code="500">Returns an error response with an array of errors in the event of an exception or other server error</response>
        [HttpGet("campaign-map")]
        [ProducesResponseType(typeof(ICampaignMapBiomeCollection), 200)]
        [ProducesResponseType(typeof(IApiErrorResult), 500)]
        public async Task<IActionResult> GetCampaignMapBiomesAsync([FromServices] IMediator mediator, CancellationToken cancelToken)
        {
            ServiceResult<IReadOnlyCollection<CampaignMapBiome>> response = await mediator.Send(new GetCampaignMapBiomesRequest(), cancelToken);

            return response.ToActionResult(contract =>
            {
                return new DTCampaignMapBiomeCollection
                {
                    Biomes = response.SuccessValue?.Select(b => new DTCampaignMapBiome
                    {
                        BiomeType = b.BiomeType,
                        Name = b.Name,
                        MovementCost = b.MovementCost,
                        DoesBlockSight = b.DoesBlockSight,
                        IsWater = b.IsWater,
                        IsTraversable = b.IsTraversable,
                        RegenerationRate = b.RegenerationRate,
                        MapEditorHtmlColor = b.MapEditorHtmlColor,
                        CanBeElevated = b.CanBeElevated,
                        IsPaintableOnCampaignMap = b.IsPaintableOnCampaignMap,
                        BaseForageValue = b.BaseForageValue,
                        BaseHuntValue = b.BaseHuntValue,
                        BaseFishValue = b.BaseFishValue,
                        BaseFodderValue = b.BaseFodderValue,
                        BaseBoneValue = b.BaseBoneValue,
                        BaseWoodValue = b.BaseWoodValue,
                        BaseStoneValue = b.BaseStoneValue,
                        BaseSpecialValue = b.BaseSpecialValue,
                    }).ToList() ?? []
                };
            });
        }
    }
}
