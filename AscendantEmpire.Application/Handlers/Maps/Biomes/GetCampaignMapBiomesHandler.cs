using AcendantEmpire.BackendContracts.Biomes;

using AscendantEmpire.Application.Data;
using AscendantEmpire.Domain.Enums.Maps;

using MediatR;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

using static AscendantEmpire.Application.Common.ServiceResultBase;

namespace AscendantEmpire.Application.Handlers.Maps.Biomes;

public record GetCampaignMapBiomesRequest() : IRequest<ServiceResult<IReadOnlyCollection<CampaignMapBiome>>>;

internal class GetCampaignMapBiomesHandler(
    ILogger<GetCampaignMapBiomesHandler> logger,
    IDbContextFactory<AscendantEmpireContext> dbFactory
) : IRequestHandler<GetCampaignMapBiomesRequest, ServiceResult<IReadOnlyCollection<CampaignMapBiome>>>
{
    public async Task<ServiceResult<IReadOnlyCollection<CampaignMapBiome>>> Handle(GetCampaignMapBiomesRequest request, CancellationToken cancellationToken)
    {
        try
        {
            await using var context = await dbFactory.CreateDbContextAsync(cancellationToken);

            var biomes = await context.CampaignBiomes
                .AsNoTracking()
                .Select(b => new CampaignMapBiome
                {
                    BiomeType = (BiomeType)b.BiomeTypeId,
                    Name = b.Name,
                    MovementCost = b.MovementCost,
                    DoesBlockSight = b.DoesBlockSight,
                    IsWater = b.IsWater,
                    IsTraversable = b.IsTraversable,
                    RegenerationRate = b.RegenerationRate,
                    MapEditorHtmlColor = b.MapEditorHtmlColor ?? "#ffffff",
                    CanBeElevated = b.CanBeElevated,
                    IsPaintableOnCampaignMap = b.IsPaintableOnCampaignMap,
                    BaseForageValue = b.BaseForageValue,
                    BaseHuntValue = b.BaseHuntValue,
                    BaseFishValue = b.BaseFishValue,
                    BaseFodderValue = b.BaseFodderValue,
                    BaseBoneValue = b.BaseBoneValue,
                    BaseWoodValue = b.BaseWoodValue,
                    BaseStoneValue = b.BaseStoneValue,
                    BaseSpecialValue = b.BaseSpecialValue
                })
                .ToListAsync(cancellationToken);

            return ServiceResult<IReadOnlyCollection<CampaignMapBiome>>.Succeed(biomes.AsReadOnly());
        }
        catch (Exception ex)
        {
            const string error = "An error occurred while retrieving Campaign Map Biomes.";
            logger.LogError(ex, error);

            return ServiceResult<IReadOnlyCollection<CampaignMapBiome>>.Fail(error);
        }
    }
}
