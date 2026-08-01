using AscendantEmpire.Application.Data;
using AscendantEmpire.Domain.Entities.Maps.CampaignMaps;
using AscendantEmpire.Domain.Enums.Maps;

using Microsoft.EntityFrameworkCore;

namespace AscendantEmpire.Tests.Handlers.MapGallery;

/// <summary>
/// Seeds the small 2×2 map that the start-position handler tests arrange against.
/// </summary>
/// <remarks>
/// Shared by the read tests (in-memory tier) and the write tests (containerised tier, because the Put handler uses raw SQL inside a transaction), so it
/// takes the context factory rather than hanging off either test base.
/// </remarks>
internal static class StartPositionsMapSeed
{
    /// <summary>
    /// Adds a Grassland and an Ocean biome, a Flat geography, and a complete 2×2 all-Grassland map.
    /// </summary>
    /// <returns>The Id of the seeded <see cref="CampaignMapGalleryIndex"/>.</returns>
    public static async Task<int> SeedAsync(IDbContextFactory<AscendantEmpireContext> dbFactory)
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
                RegenerationRate = 1.0m,
                IsPaintableOnCampaignMap = true,
                MapEditorHtmlColor = "#90ee90",
                CanBeElevated = true
            },
            // Present so a write test can flip a tile to water and prove the handler rejects it.
            new CampaignBiome
            {
                BiomeTypeId = BiomeType.Ocean,
                Name = "Ocean",
                MovementCost = 1.0m,
                DoesBlockSight = false,
                IsWater = true,
                IsTraversable = false,
                RegenerationRate = 1.0m,
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
            MapName = "Test Map",
            Description = "Test",
            Width = 2,
            Height = 2,
            CreationStatus = MapCreationStatus.Complete,
            ProgressPercent = 100,
            CreationEngine = MapCreationEngine.BlankMap,
            Status = "Complete",
            PropertiesJson = "{}",
            CampaignMapOffsetId = 1,
            HorizontalWrap = false,
            VerticalWrap = false
        };

        context.CampaignMapGalleryIndex.Add(map);
        await context.SaveChangesAsync();

        CampaignBiome grassland = await context.CampaignBiomes.SingleAsync(x => x.BiomeTypeId == BiomeType.Grassland);
        CampaignGeography flat = await context.CampaignGeographies.SingleAsync(x => x.CampaignMapGeographyTypeId == CampaignMapGeographyType.Flat);

        int tileId = 1;
        for (int y = 0; y < 2; y++)
        {
            for (int x = 0; x < 2; x++)
            {
                context.CampaignMapGallery.Add(new CampaignMapGallery
                {
                    CampaignMapGalleryId = tileId++,
                    CampaignMapGalleryIndexId = map.Id,
                    XCoord = x,
                    YCoord = y,
                    BiomeTypeId = BiomeType.Grassland,
                    CampaignMapGeographyTypeId = CampaignMapGeographyType.Flat,
                    Biome = grassland,
                    Geography = flat,
                    Index = map
                });
            }
        }

        await context.SaveChangesAsync();
        return map.Id;
    }
}
