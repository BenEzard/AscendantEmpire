using AscendantEmpire.Application.Common.Messaging;
using AscendantEmpire.Application.Data;
using AscendantEmpire.Application.Features.Maps.MapGallery.Classes;
using AscendantEmpire.Domain.Entities.Maps.CampaignMaps;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

using static AscendantEmpire.Application.Common.ServiceResultBase;

namespace AscendantEmpire.Application.Features.Maps.MapGallery.Handlers;

public record GetCampaignMapTilesRequest(int MapId) : IRequest<ServiceResult<IReadOnlyCollection<DTCampaignMapTile>>>;

internal class GetCampaignMapTilesHandler(
    ILogger<GetCampaignMapTilesHandler> logger,
    AscendantEmpireContext context
) : IRequestHandler<GetCampaignMapTilesRequest, ServiceResult<IReadOnlyCollection<DTCampaignMapTile>>>
{
    public async Task<ServiceResult<IReadOnlyCollection<DTCampaignMapTile>>> Handle(GetCampaignMapTilesRequest request, CancellationToken cancellationToken)
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

        // The river columns go out raw, as the two edges a tile actually owns. The map editor edits those columns, so resolving them into four
        // per-tile edges the way the player map read does would only mean the editor translating them back again before it could save anything.
        List<DTCampaignMapTile> data = [.. entities.Select(tile => new DTCampaignMapTile
        {
            CampaignMapGalleryIndexId = tile.CampaignMapGalleryIndexId,
            XCoord = tile.XCoord,
            YCoord = tile.YCoord,
            BiomeTypeId = tile.BiomeTypeId,
            CampaignMapGeographyTypeId = tile.CampaignMapGeographyTypeId,
            SouthRiver = tile.SouthRiver,
            EastRiver = tile.EastRiver
        })];

        return ServiceResult<IReadOnlyCollection<DTCampaignMapTile>>.Succeed(data);
    }
}
