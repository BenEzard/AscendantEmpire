using AscendantEmpire.Application.Common.Messaging;
using AscendantEmpire.Application.Data;
using AscendantEmpire.BackendContracts.Geographies;
using AscendantEmpire.Domain.Enums.Maps;

using Microsoft.EntityFrameworkCore;

using static AscendantEmpire.Application.Common.ServiceResultBase;

namespace AscendantEmpire.Application.Features.Maps.Geographies.Handlers;

public record GetCampaignMapGeographiesRequest() : IRequest<ServiceResult<IReadOnlyCollection<CampaignMapGeography>>>;

public class GetCampaignMapGeographiesHandler(
    IDbContextFactory<AscendantEmpireContext> dbFactory
    ) : IRequestHandler<GetCampaignMapGeographiesRequest, ServiceResult<IReadOnlyCollection<CampaignMapGeography>>>
{
    public async Task<ServiceResult<IReadOnlyCollection<CampaignMapGeography>>> Handle(GetCampaignMapGeographiesRequest request, CancellationToken cancellationToken)
    {
        await using var context = await dbFactory.CreateDbContextAsync(cancellationToken);

        List<CampaignMapGeography> geographies = await context.CampaignGeographies
            .AsNoTracking()
            .Select(g => new CampaignMapGeography
            {
                CampaignMapGeographyType = (CampaignMapGeographyType)g.CampaignMapGeographyTypeId,
                RelativeElevation = g.RelativeElevation,
                Name = g.Name,
                MovementCost = g.MovementCost,
                IsWater = g.IsWater,
                IsTraversable = g.IsTraversable,
                MapEditorIcon = g.MapEditorIcon,
                IsPaintableOnCampaignMap = g.IsPaintableOnCampaignMap
            })
            .ToListAsync(cancellationToken);

        return ServiceResult<IReadOnlyCollection<CampaignMapGeography>>.Succeed(geographies.AsReadOnly());
    }
}
