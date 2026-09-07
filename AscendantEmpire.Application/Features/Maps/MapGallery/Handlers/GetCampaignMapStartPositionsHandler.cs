using AscendantEmpire.Application.Common.Messaging;
using AscendantEmpire.Application.Data;
using AscendantEmpire.Application.Features.Maps.MapGallery.Classes;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

using static AscendantEmpire.Application.Common.ServiceResultBase;

namespace AscendantEmpire.Application.Features.Maps.MapGallery.Handlers;

public record GetCampaignMapStartPositionsRequest(int MapId) : IRequest<ServiceResult<IReadOnlyCollection<DTCampaignMapStartPosition>>>;

internal class GetCampaignMapStartPositionsHandler(
    ILogger<GetCampaignMapStartPositionsHandler> logger,
    AscendantEmpireContext context
) : IRequestHandler<GetCampaignMapStartPositionsRequest, ServiceResult<IReadOnlyCollection<DTCampaignMapStartPosition>>>
{
    public async Task<ServiceResult<IReadOnlyCollection<DTCampaignMapStartPosition>>> Handle(GetCampaignMapStartPositionsRequest request, CancellationToken cancellationToken)
    {
        try
        {
            bool mapExists = await context.CampaignMapGalleryIndex
                .AsNoTracking()
                .AnyAsync(x => x.Id == request.MapId, cancellationToken);

            if (!mapExists)
            {
                const string error = "Campaign Map Gallery Index not found.";
                logger.LogWarning("{Error} MapId: {MapId}", error, request.MapId);
                return ServiceResult<IReadOnlyCollection<DTCampaignMapStartPosition>>.NotFound(error);
            }

            List<DTCampaignMapStartPosition> startPositions = await context.CampaignMapStartPositions
                .AsNoTracking()
                .Where(x => x.CampaignMapGalleryIndexId == request.MapId)
                .OrderBy(x => x.YCoord)
                .ThenBy(x => x.XCoord)
                .ThenBy(x => x.Id)
                .Select(x => new DTCampaignMapStartPosition
                {
                    Id = x.Id,
                    CampaignMapGalleryIndexId = x.CampaignMapGalleryIndexId,
                    XCoord = x.XCoord,
                    YCoord = x.YCoord,
                    DisplayName = x.DisplayName,
                    LocationScore = x.LocationScore
                })
                .ToListAsync(cancellationToken);

            return ServiceResult<IReadOnlyCollection<DTCampaignMapStartPosition>>.Succeed(startPositions);
        }
        catch (Exception ex)
        {
            const string error = "An error occurred while retrieving the Campaign Map start positions.";
            logger.LogError(ex, error + " MapId: {MapId}", request.MapId);
            return ServiceResult<IReadOnlyCollection<DTCampaignMapStartPosition>>.InternalServerError(error);
        }
    }
}
