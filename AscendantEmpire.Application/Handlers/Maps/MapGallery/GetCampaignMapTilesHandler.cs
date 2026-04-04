using AscendantEmpire.Application.Data;
using AscendantEmpire.Application.Handlers.Maps.MapGallery.Classes;
using AscendantEmpire.Domain.Entities.Maps.CampaignMaps;

using MediatR;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

using static AscendantEmpire.Application.Common.ServiceResultBase;

namespace AscendantEmpire.Application.Handlers.Maps.MapGallery;

public record GetCampaignMapTilesRequest(int MapId) : IRequest<ServiceResult<IReadOnlyCollection<DTCampaignMapTile>>>;

internal class GetCampaignMapTilesHandler(
    ILogger<GetCampaignMapTilesHandler> logger,
    AscendantEmpireContext context
) : IRequestHandler<GetCampaignMapTilesRequest, ServiceResult<IReadOnlyCollection<DTCampaignMapTile>>>
{
    public async Task<ServiceResult<IReadOnlyCollection<DTCampaignMapTile>>> Handle(GetCampaignMapTilesRequest request, CancellationToken cancellationToken)
    {
        try
        {
            IEnumerable<CampaignMapGallery> entities = await context.CampaignMapGallery
                .AsNoTracking()
                .Where(t => t.CampaignMapGalleryIndexId == request.MapId)
                .ToListAsync(cancellationToken);

            if (entities.Any() == false)
            {
                const string notFoundError = "No Campaign Map Tiles found for the specified Map ID.";
                logger.LogWarning(notFoundError + " MapId: {MapId}", request.MapId);
                return ServiceResult<IReadOnlyCollection<DTCampaignMapTile>>.NotFound(notFoundError);
            }

            // Assuming we found some tiles, we would map them to DTCampaignMapTile DTOs.
            // Here we just return a dummy tile for demonstration.
            List<DTCampaignMapTile> data = [.. entities.Select(tile => new DTCampaignMapTile
            {
                CampaignMapGalleryIndexId = tile.CampaignMapGalleryIndexId,
                XCoord = tile.XCoord,
                YCoord = tile.YCoord,
                BiomeTypeId = tile.BiomeTypeId,
                CampaignMapGeographyTypeId = tile.CampaignMapGeographyTypeId
            })];

            return ServiceResult<IReadOnlyCollection<DTCampaignMapTile>>.Succeed(data);
        }
        catch (Exception ex)
        {
            const string error = "An error occurred while retrieving the Campaign Map Tiles.";
            logger.LogError(ex, error + " MapId: {MapId}", request.MapId);
            return ServiceResult<IReadOnlyCollection<DTCampaignMapTile>>.InternalServerError(error);
        }
    }
}
