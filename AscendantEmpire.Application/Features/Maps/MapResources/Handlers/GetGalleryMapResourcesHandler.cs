using AscendantEmpire.Application.Common.Messaging;
using AscendantEmpire.Application.Data;
using AscendantEmpire.Application.Features.Maps.MapResources.Services;
using AscendantEmpire.BackendContracts.MapResources.GalleryPlacements;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

using static AscendantEmpire.Application.Common.ServiceResultBase;

namespace AscendantEmpire.Application.Features.Maps.MapResources.Handlers;

public record GetGalleryMapResourcesRequest(int CampaignMapGalleryIndexId) : IRequest<ServiceResult<IReadOnlyCollection<GalleryMapResource>>>;

/// <summary>
/// Lists the MapResources placed on one gallery map, for the map editor to draw and edit.
/// </summary>
internal class GetGalleryMapResourcesHandler(
    ILogger<GetGalleryMapResourcesHandler> logger,
    IDbContextFactory<AscendantEmpireContext> dbFactory
) : IRequestHandler<GetGalleryMapResourcesRequest, ServiceResult<IReadOnlyCollection<GalleryMapResource>>>
{
    public async Task<ServiceResult<IReadOnlyCollection<GalleryMapResource>>> Handle(GetGalleryMapResourcesRequest request, CancellationToken cancellationToken)
    {
        try
        {
            await using AscendantEmpireContext context = await dbFactory.CreateDbContextAsync(cancellationToken);

            if (await context.CampaignMapGalleryIndex.AnyAsync(m => m.Id == request.CampaignMapGalleryIndexId, cancellationToken) == false)
            {
                return ServiceResult<IReadOnlyCollection<GalleryMapResource>>.NotFound("Map Gallery entry not found.");
            }

            List<GalleryMapResource> placements = await GalleryMapResourceQueries.ReadAsync(context, request.CampaignMapGalleryIndexId, placementId: null, cancellationToken);

            return ServiceResult<IReadOnlyCollection<GalleryMapResource>>.Succeed(placements);
        }
        catch (Exception ex)
        {
            const string error = "An error occurred while retrieving the map's MapResources.";
            logger.LogError(ex, error);

            return ServiceResult<IReadOnlyCollection<GalleryMapResource>>.Fail(error);
        }
    }
}
