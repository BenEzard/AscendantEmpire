using AscendantEmpire.Application.Common.Messaging;
using AscendantEmpire.Application.Data;
using AscendantEmpire.Application.Features.Maps.MapResources.Services;
using AscendantEmpire.BackendContracts.MapResources.GalleryPlacements;
using AscendantEmpire.Domain.Entities.Maps.CampaignMaps;
using AscendantEmpire.FrontendContracts.MapResources.GalleryPlacements;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

using static AscendantEmpire.Application.Common.ServiceResultBase;

namespace AscendantEmpire.Application.Features.Maps.MapResources.Handlers;

public record PlaceGalleryMapResourceRequest(int CampaignMapGalleryIndexId, IPlaceGalleryMapResourceDto Placement) : IRequest<ServiceResult<GalleryMapResource>>;

/// <summary>
/// Places a MapResource on a gallery map tile, in the tile's first free slot, with its specifics left to be decided when a game is created.
/// </summary>
/// <remarks>
/// Refused when the type has no habitat for the tile's Biome and Geography (it could never exist there), when the tile already holds that type, or when both
/// of the tile's slots are taken. The database enforces the last two as well; checking here turns them into messages the editor can show.
/// </remarks>
internal class PlaceGalleryMapResourceHandler(
    ILogger<PlaceGalleryMapResourceHandler> logger,
    AscendantEmpireContext context
) : IRequestHandler<PlaceGalleryMapResourceRequest, ServiceResult<GalleryMapResource>>
{
    public async Task<ServiceResult<GalleryMapResource>> Handle(PlaceGalleryMapResourceRequest request, CancellationToken cancellationToken)
    {
        IPlaceGalleryMapResourceDto placement = request.Placement;

        try
        {
            CampaignMapGallery? tile = await context.CampaignMapGallery
                .AsNoTracking()
                .SingleOrDefaultAsync(t => t.CampaignMapGalleryIndexId == request.CampaignMapGalleryIndexId && t.XCoord == placement.XCoord && t.YCoord == placement.YCoord, cancellationToken);

            if (tile == null)
            {
                return ServiceResult<GalleryMapResource>.NotFound($"The map has no tile at ({placement.XCoord},{placement.YCoord}).");
            }

            var type = await context.MapResourceTypes
                .AsNoTracking()
                .Where(t => t.Id == placement.MapResourceTypeId)
                .Select(t => new { t.DisplayName })
                .SingleOrDefaultAsync(cancellationToken);

            if (type == null)
            {
                return ServiceResult<GalleryMapResource>.NotFound($"There is no MapResourceType {placement.MapResourceTypeId}.");
            }

            // No habitat for the tile's Biome (Biome-wide, or naming its Geography) means the type can never exist there.
            bool compatible = await context.MapResourceHabitats.AnyAsync(h =>
                h.MapResourceTypeId == placement.MapResourceTypeId
                && h.BiomeTypeId == tile.BiomeTypeId
                && (h.CampaignMapGeographyTypeId == null || h.CampaignMapGeographyTypeId == tile.CampaignMapGeographyTypeId),
                cancellationToken);

            if (compatible == false)
            {
                return ServiceResult<GalleryMapResource>.Fail($"{type.DisplayName} has no habitat for this tile's Biome and Geography, so it cannot exist here.");
            }

            List<(byte Slot, int TypeId)> onTile = [.. (await context.CampaignMapGalleryResources
                .Where(p => p.CampaignMapGalleryId == tile.CampaignMapGalleryId)
                .Select(p => new { p.Slot, p.MapResourceTypeId })
                .ToListAsync(cancellationToken))
                .Select(p => (p.Slot, p.MapResourceTypeId))];

            if (onTile.Any(p => p.TypeId == placement.MapResourceTypeId))
            {
                return ServiceResult<GalleryMapResource>.Fail($"This tile already has {type.DisplayName}.");
            }

            byte? freeSlot = Enumerable.Range(CampaignMapGalleryResource.MIN_SLOT, CampaignMapGalleryResource.MAX_SLOT)
                .Select(slot => (byte)slot)
                .Where(slot => onTile.Any(p => p.Slot == slot) == false)
                .Select(slot => (byte?)slot)
                .FirstOrDefault();

            if (freeSlot == null)
            {
                return ServiceResult<GalleryMapResource>.Fail($"A tile holds at most {CampaignMapGalleryResource.MAX_SLOT} MapResources, and this one is full.");
            }

            var entity = new CampaignMapGalleryResource
            {
                CampaignMapGalleryId = tile.CampaignMapGalleryId,
                MapResourceTypeId = placement.MapResourceTypeId,
                Slot = freeSlot.Value
            };

            context.CampaignMapGalleryResources.Add(entity);
            await context.SaveChangesAsync(CancellationToken.None);

            List<GalleryMapResource> saved = await GalleryMapResourceQueries.ReadAsync(context, request.CampaignMapGalleryIndexId, entity.Id, cancellationToken);

            return ServiceResult<GalleryMapResource>.Created(saved.Single());
        }
        catch (Exception ex)
        {
            const string error = "Error placing the MapResource.";
            logger.LogError(ex, error);
            return ServiceResult<GalleryMapResource>.Fail(error);
        }
    }
}
