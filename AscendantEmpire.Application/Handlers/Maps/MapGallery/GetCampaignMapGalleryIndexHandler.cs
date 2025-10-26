using AscendantEmpire.Application.Data;
using AscendantEmpire.Domain.Entities.Maps.CampaignMaps;

using MediatR;

using Microsoft.Extensions.Logging;

using static AscendantEmpire.Application.Common.ServiceResultBase;

namespace AscendantEmpire.Application.Handlers.Maps.MapGallery;

public record GetCampaignMapGalleryIndexRequest(int MapId) : IRequest<ServiceResult<CampaignMapGalleryIndex>>;

internal class GetCampaignMapGalleryIndexHandler(
    ILogger<GetCampaignMapGalleryIndexHandler> logger,
    AscendantEmpireContext context
) : IRequestHandler<GetCampaignMapGalleryIndexRequest, ServiceResult<CampaignMapGalleryIndex>>
{
    public async Task<ServiceResult<CampaignMapGalleryIndex>> Handle(GetCampaignMapGalleryIndexRequest request, CancellationToken cancellationToken)
    {
        try
        {
            CampaignMapGalleryIndex? mapIndex = await context.CampaignMapGalleryIndex.FindAsync([request.MapId], cancellationToken);

            if (mapIndex == null)
            {
                const string error = "Campaign Map Gallery Index not found.";
                logger.LogWarning(error + " MapId: {MapId}", request.MapId);
                return ServiceResult<CampaignMapGalleryIndex>.Fail(error);
            }

            return ServiceResult<CampaignMapGalleryIndex>.Succeed(mapIndex);
        }
        catch (Exception ex)
        {
            const string error = "An error occurred while retrieving the Campaign Map Gallery Index.";
            logger.LogError(ex, error + " MapId: {MapId}", request.MapId);
            return ServiceResult<CampaignMapGalleryIndex>.Fail(error);
        }
    }
}
