using AscendantEmpire.Application.Common.Messaging;
using AscendantEmpire.Application.Data;
using AscendantEmpire.Application.Helpers.ScaledNumerics;
using AscendantEmpire.BackendContracts.Biomes;
using AscendantEmpire.Domain.Enums.Maps;

using Microsoft.EntityFrameworkCore;

using static AscendantEmpire.Application.Common.ServiceResultBase;

namespace AscendantEmpire.Application.Features.Maps.Biomes.Handlers;

public record GetCampaignMapBiomesRequest() : IRequest<ServiceResult<IReadOnlyCollection<CampaignMapBiome>>>;

internal class GetCampaignMapBiomesHandler(
    IDbContextFactory<AscendantEmpireContext> dbFactory
) : IRequestHandler<GetCampaignMapBiomesRequest, ServiceResult<IReadOnlyCollection<CampaignMapBiome>>>
{
    public async Task<ServiceResult<IReadOnlyCollection<CampaignMapBiome>>> Handle(GetCampaignMapBiomesRequest request, CancellationToken cancellationToken)
    {
        await using var context = await dbFactory.CreateDbContextAsync(cancellationToken);

        // Materialize first: the base values are stored scaled, and the scaled -> whole conversion
        // below cannot be translated to SQL, so it must run in memory after the query.
        var biomes = (await context.CampaignBiomes
            .AsNoTracking()
            .ToListAsync(cancellationToken))
            .Select(b => new CampaignMapBiome
            {
                BiomeType = (BiomeType)b.BiomeTypeId,
                Name = b.Name,
                MovementCost = b.MovementCost,
                DoesBlockSight = b.DoesBlockSight,
                IsWater = b.IsWater,
                IsTraversable = b.IsTraversable,
                RegenerationPercent = b.RegenerationPercent,
                MapEditorHtmlColor = b.MapEditorHtmlColor ?? "#ffffff",
                CanBeElevated = b.CanBeElevated,
                IsPaintableOnCampaignMap = b.IsPaintableOnCampaignMap,
                // Convert the scaled storage values back to the whole numbers the UI expects.
                BaseForageValue = ScaledResourceValue.ToWhole(b.BaseForageValue),
                BaseHuntValue = ScaledResourceValue.ToWhole(b.BaseHuntValue),
                BaseFishValue = ScaledResourceValue.ToWhole(b.BaseFishValue),
                BaseFodderValue = ScaledResourceValue.ToWhole(b.BaseFodderValue),
                BaseBoneValue = ScaledResourceValue.ToWhole(b.BaseBoneValue),
                BaseWoodValue = ScaledResourceValue.ToWhole(b.BaseWoodValue),
                BaseStoneValue = ScaledResourceValue.ToWhole(b.BaseStoneValue)
            })
            .ToList();

        return ServiceResult<IReadOnlyCollection<CampaignMapBiome>>.Succeed(biomes.AsReadOnly());
    }
}
