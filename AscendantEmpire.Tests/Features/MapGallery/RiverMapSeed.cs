using AscendantEmpire.Application.Data;
using AscendantEmpire.Domain.Entities.Maps.CampaignMaps;
using AscendantEmpire.Domain.Enums.Maps;

using Microsoft.EntityFrameworkCore;

namespace AscendantEmpire.Tests.Features.MapGallery;

/// <summary>
/// Seeds the small coastal map the river authoring tests arrange against.
/// </summary>
/// <remarks>
/// <para>
/// Five wide by four high, with <b>row 0 all Ocean</b> and the three rows below it all Grassland. That gives a coast along the top for rivers to run
/// from, and enough land under it for a river to reach well inland of the tiles the coast touches.
/// </para>
/// <para>
/// Tile ids run row-major from <see cref="CampaignMapOffsetId"/>, the same layout the generators produce, so the handler's coordinate-to-id arithmetic
/// is exercised rather than worked around.
/// </para>
/// </remarks>
internal static class RiverMapSeed
{
    internal const int MAP_WIDTH = 5;
    internal const int MAP_HEIGHT = 4;

    /// <summary>The id of the tile at (0, 0). Deliberately not 1, so a test would catch the offset being ignored.</summary>
    internal const int CampaignMapOffsetId = 500;

    /// <summary>
    /// Adds an Ocean and a Grassland Biome, a Flat Geography, and a complete 5 x 4 map with a sea along its top row.
    /// </summary>
    /// <param name="dbFactory">The context factory for the test's own store.</param>
    /// <param name="horizontalWrap">Whether the map's east and west edges join.</param>
    /// <param name="verticalWrap">Whether the map's north and south edges join.</param>
    /// <returns>The Id of the seeded <see cref="CampaignMapGalleryIndex"/>.</returns>
    internal static async Task<int> SeedAsync(
        IDbContextFactory<AscendantEmpireContext> dbFactory,
        bool horizontalWrap = false,
        bool verticalWrap = false)
    {
        await using AscendantEmpireContext context = await dbFactory.CreateDbContextAsync();

        context.CampaignBiomes.AddRange(
            new CampaignBiome
            {
                BiomeTypeId = BiomeType.Grassland,
                Name = "Grassland",
                MovementCost = 1.0m,
                DoesBlockSight = false,
                IsWater = false,
                IsTraversable = true,
                RegenerationPercent = 0.1m,
                IsPaintableOnCampaignMap = true,
                MapEditorHtmlColor = "#90ee90",
                CanBeElevated = true
            },
            new CampaignBiome
            {
                BiomeTypeId = BiomeType.Ocean,
                Name = "Ocean",
                MovementCost = 1.0m,
                DoesBlockSight = false,
                IsWater = true,
                IsTraversable = false,
                RegenerationPercent = 0.1m,
                IsPaintableOnCampaignMap = true,
                MapEditorHtmlColor = "#0000ff",
                CanBeElevated = false
            });

        context.CampaignGeographies.Add(new CampaignGeography
        {
            CampaignMapGeographyTypeId = CampaignMapGeographyType.Flat,
            Name = "Flat",
            RelativeElevation = 0,
            MovementCost = 1.0m,
            MapEditorIcon = null,
            IsPaintableOnCampaignMap = true
        });

        CampaignMapGalleryIndex map = new()
        {
            MapName = "River Test Map",
            Description = "A coast along the top and land below it.",
            Width = MAP_WIDTH,
            Height = MAP_HEIGHT,
            CreationStatus = MapCreationStatus.Complete,
            ProgressPercent = 100,
            CreationEngine = MapCreationEngine.BlankMap,
            Status = "Complete",
            PropertiesJson = "{}",
            CampaignMapOffsetId = CampaignMapOffsetId,
            HorizontalWrap = horizontalWrap,
            VerticalWrap = verticalWrap
        };

        context.CampaignMapGalleryIndex.Add(map);
        await context.SaveChangesAsync();

        CampaignBiome grassland = await context.CampaignBiomes.SingleAsync(x => x.BiomeTypeId == BiomeType.Grassland);
        CampaignBiome ocean = await context.CampaignBiomes.SingleAsync(x => x.BiomeTypeId == BiomeType.Ocean);
        CampaignGeography flat = await context.CampaignGeographies.SingleAsync(x => x.CampaignMapGeographyTypeId == CampaignMapGeographyType.Flat);

        for (int y = 0; y < MAP_HEIGHT; y++)
        {
            for (int x = 0; x < MAP_WIDTH; x++)
            {
                bool isSea = y == 0;

                context.CampaignMapGallery.Add(new CampaignMapGallery
                {
                    CampaignMapGalleryId = CampaignMapOffsetId + x + (y * MAP_WIDTH),
                    CampaignMapGalleryIndexId = map.Id,
                    XCoord = x,
                    YCoord = y,
                    BiomeTypeId = isSea ? BiomeType.Ocean : BiomeType.Grassland,
                    CampaignMapGeographyTypeId = CampaignMapGeographyType.Flat,
                    Biome = isSea ? ocean : grassland,
                    Geography = flat,
                    Index = map
                });
            }
        }

        await context.SaveChangesAsync();
        return map.Id;
    }
}
