using AscendantEmpire.Application.Common.Messaging;
using AscendantEmpire.Application.Data;
using AscendantEmpire.BackendContracts.Geographies;
using AscendantEmpire.Domain.Enums.Maps;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

using static AscendantEmpire.Application.Common.ServiceResultBase;

namespace AscendantEmpire.Application.Features.Maps.Geographies.Handlers;

public record GetCampaignMapGeographiesRequest() : IRequest<ServiceResult<IReadOnlyCollection<CampaignMapGeography>>>;

public class GetCampaignMapGeographiesHandler(
    ILogger<GetCampaignMapGeographiesHandler> logger,
    IDbContextFactory<AscendantEmpireContext> dbFactory
    ) : IRequestHandler<GetCampaignMapGeographiesRequest, ServiceResult<IReadOnlyCollection<CampaignMapGeography>>>
{
    public async Task<ServiceResult<IReadOnlyCollection<CampaignMapGeography>>> Handle(GetCampaignMapGeographiesRequest request, CancellationToken cancellationToken)
    {
        try
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
                    MapEditorIcon = g.MapEditorIcon,
                    IsPaintableOnCampaignMap = g.IsPaintableOnCampaignMap
                })
                .ToListAsync(cancellationToken);

            return ServiceResult<IReadOnlyCollection<CampaignMapGeography>>.Succeed(geographies.AsReadOnly());
        }
        catch (Exception ex)
        {
            const string error = "An error occurred while retrieving Campaign Map Geographies.";
            logger.LogError(ex, error);

            return ServiceResult<IReadOnlyCollection<CampaignMapGeography>>.Fail(error);
        }
    }
}
