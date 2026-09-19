using AscendantEmpire.Application.Data;
using AscendantEmpire.Application.Helpers.ScaledNumerics;
using AscendantEmpire.BackendContracts.MapResources.GalleryPlacements;

using Microsoft.EntityFrameworkCore;

namespace AscendantEmpire.Application.Features.Maps.MapResources.Services;

/// <summary>
/// Reads MapResource placements on gallery maps in the shape the map editor uses: with the tile's coordinates and the pinned scenario's number rather than
/// the ids they are stored by.
/// </summary>
internal static class GalleryMapResourceQueries
{
    /// <summary>
    /// The placements on one gallery map, optionally narrowed to one placement. Read without tracking.
    /// </summary>
    public static async Task<List<GalleryMapResource>> ReadAsync(AscendantEmpireContext context, int campaignMapGalleryIndexId, int? placementId, CancellationToken cancellationToken)
    {
        // Placements carry no navigations, so the tile and the scenario are joined by hand.
        var rows = await (
            from placement in context.CampaignMapGalleryResources.AsNoTracking()
            join tile in context.CampaignMapGallery on placement.CampaignMapGalleryId equals tile.CampaignMapGalleryId
            where tile.CampaignMapGalleryIndexId == campaignMapGalleryIndexId
            where placementId == null || placement.Id == placementId
            orderby tile.YCoord, tile.XCoord, placement.Slot
            select new
            {
                placement.Id,
                tile.XCoord,
                tile.YCoord,
                placement.Slot,
                placement.MapResourceTypeId,
                placement.InitialQuantity,
                ScenarioNumber = context.MapResourceDistributionScenarios
                    .Where(s => s.Id == placement.MapResourceDistributionScenarioId)
                    .Select(s => (int?)s.ScenarioNumber)
                    .FirstOrDefault()
            })
            .ToListAsync(cancellationToken);

        // The whole-number conversion cannot be translated to SQL, so it runs after the query.
        return [.. rows.Select(row => new GalleryMapResource
        {
            Id = row.Id,
            XCoord = row.XCoord,
            YCoord = row.YCoord,
            Slot = row.Slot,
            MapResourceTypeId = row.MapResourceTypeId,
            InitialQuantity = ScaledResourceValue.ToWhole(row.InitialQuantity),
            DistributionScenarioNumber = row.ScenarioNumber
        })];
    }
}
