using AscendantEmpire.Application.Data;
using AscendantEmpire.Application.Handlers.Maps.MapGallery.Classes;
using AscendantEmpire.Domain.Entities.Maps.CampaignMaps;

using MediatR;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

using static AscendantEmpire.Application.Common.ServiceResultBase;

namespace AscendantEmpire.Application.Handlers.Maps.MapGallery;

public record GetCampaignMapGalleryIndexRequest(int MapId) : IRequest<ServiceResult<DTCampaignMapGalleryIndex>>;

internal class GetCampaignMapGalleryIndexHandler(
    ILogger<GetCampaignMapGalleryIndexHandler> logger,
    AscendantEmpireContext context
) : IRequestHandler<GetCampaignMapGalleryIndexRequest, ServiceResult<DTCampaignMapGalleryIndex>>
{
    public async Task<ServiceResult<DTCampaignMapGalleryIndex>> Handle(GetCampaignMapGalleryIndexRequest request, CancellationToken cancellationToken)
    {
        try
        {
            // Prefer AsNoTracking for read models (FindAsync will track)
            CampaignMapGalleryIndex? entity = await context.CampaignMapGalleryIndex
                .AsNoTracking()
                .FirstOrDefaultAsync(x => x.Id == request.MapId, cancellationToken);

            if (entity is null)
            {
                const string error = "Campaign Map Gallery Index not found.";
                logger.LogWarning("{Error} MapId: {MapId}", error, request.MapId);
                return ServiceResult<DTCampaignMapGalleryIndex>.NotFound(error);
            }

            var dto = new DTCampaignMapGalleryIndex
            {
                CampaignMapGalleryIndexId = entity.Id,
                MapName = entity.MapName,
                Description = entity.Description,
                Width = entity.Width,
                Height = entity.Height,
                CampaignMapOffsetId = entity.CampaignMapOffsetId,
                CreationStatus = entity.CreationStatus,
                CreationEngine = entity.CreationEngine,
                PropertiesJson = entity.PropertiesJson,
                Status = entity.Status,
                ErrorMessage = entity.ErrorMessage
            };

            return ServiceResult<DTCampaignMapGalleryIndex>.Succeed(dto);
        }
        catch (Exception ex)
        {
            const string error = "An error occurred while retrieving the Campaign Map Gallery Index.";
            logger.LogError(ex, error + " MapId: {MapId}", request.MapId);
            return ServiceResult<DTCampaignMapGalleryIndex>.InternalServerError(error);
        }
    }
}
