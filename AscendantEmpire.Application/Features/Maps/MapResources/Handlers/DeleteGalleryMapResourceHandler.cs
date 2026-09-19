using AscendantEmpire.Application.Common;
using AscendantEmpire.Application.Common.Messaging;
using AscendantEmpire.Application.Data;
using AscendantEmpire.Domain.Entities.Maps.CampaignMaps;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace AscendantEmpire.Application.Features.Maps.MapResources.Handlers;

public record DeleteGalleryMapResourceRequest(int CampaignMapGalleryIndexId, int PlacementId) : IRequest<ServiceResultBase>;

/// <summary>
/// Removes a MapResource from a gallery map tile. Games already created from the map keep theirs.
/// </summary>
internal class DeleteGalleryMapResourceHandler(
    ILogger<DeleteGalleryMapResourceHandler> logger,
    AscendantEmpireContext context
) : IRequestHandler<DeleteGalleryMapResourceRequest, ServiceResultBase>
{
    public async Task<ServiceResultBase> Handle(DeleteGalleryMapResourceRequest request, CancellationToken cancellationToken)
    {
        try
        {
            // The placement must be on the map named in the route, so one map's editor cannot change another map.
            CampaignMapGalleryResource? placement = await (
                from p in context.CampaignMapGalleryResources
                join tile in context.CampaignMapGallery on p.CampaignMapGalleryId equals tile.CampaignMapGalleryId
                where p.Id == request.PlacementId && tile.CampaignMapGalleryIndexId == request.CampaignMapGalleryIndexId
                select p)
                .SingleOrDefaultAsync(cancellationToken);

            if (placement == null)
            {
                return ServiceResultBase.NotFound("That MapResource is not on this map.");
            }

            context.CampaignMapGalleryResources.Remove(placement);
            await context.SaveChangesAsync(CancellationToken.None);
        }
        catch (Exception ex)
        {
            const string error = "Error removing the MapResource.";
            logger.LogError(ex, error);
            return ServiceResultBase.InternalServerError(error);
        }

        return ServiceResultBase.SuccessNoContent();
    }
}
