using AscendantEmpire.Application.Data;
using AscendantEmpire.Domain.Entities.Maps.CampaignMaps;
using AscendantEmpire.Domain.Entities.Maps.MapResources;
using AscendantEmpire.Domain.Entities.Assets;
using AscendantEmpire.Domain.Enums.Maps;
using AscendantEmpire.Domain.Enums.MapResources;
using AscendantEmpire.Domain.Enums.Assets;

using Microsoft.EntityFrameworkCore;

namespace AscendantEmpire.Tests.Features.Maps.MapResources;

/// <summary>
/// The ids of the small MapResource catalogue <see cref="MapResourceTestSeed.SeedCatalogueAsync"/> writes.
/// </summary>
/// <param name="BerryTypeId">A renewable type with one yield.</param>
/// <param name="WoodTypeId">A Distribution with one scenario.</param>
/// <param name="CoalTypeId">A stratified type with one layer.</param>
/// <param name="AssetId">The one Asset every type yields or holds.</param>
/// <param name="ScenarioId">The Distribution's scenario.</param>
internal sealed record MapResourceTestCatalogue(int BerryTypeId, int WoodTypeId, int CoalTypeId, int AssetId, int ScenarioId);

/// <summary>
/// Writes the catalogue rows MapResource tests need against a real database: an Asset to point at, and one MapResourceType of each kind that holds its Assets
/// differently.
/// </summary>
/// <remarks>
/// Nothing seeds MapResources yet, and the Asset hierarchy is seeded by the Entity Setup Tool rather than a migration, so the template database has none of these. The
/// codes are prefixed "Test" so they cannot collide with anything seeded for real.
/// </remarks>
internal static class MapResourceTestSeed
{
    /// <summary>
    /// Adds the catalogue, and the Forest Biome its habitats name if the database does not already have it, and saves.
    /// </summary>
    public static async Task<MapResourceTestCatalogue> SeedCatalogueAsync(AscendantEmpireContext context)
    {
        await EnsureBiomeAsync(context, BiomeType.Forest);

        Asset asset = (await SeedAssetsAsync(context, "TestForage"))[0];

        // --- The MapResource catalogue ---
        var mapCategory = new MapResourceCategory { Code = "TestFood", DisplayName = "Test Food", SortOrder = 1 };

        var berry = new MapResourceType
        {
            Code = "TestBerry",
            DisplayName = "Test Berry",
            Category = mapCategory,
            IconPath = "/icons/berry.png",
            IsRenewable = true,
            ExtractionMethod = ExtractionMethod.Gather,
            IsDiscoverableByDefault = true,
            DiscoveryLikelihood = 1m,
            EstimationFuzzyFactorMin = 0.1m,
            EstimationFuzzyFactorMax = 0.3m,
            SurvivalRounds = 5,
            ReseedLikelihood = 0.2m,
            Yields = [new MapResourceTypeYield { AssetId = asset.Id, AmountPerUnit = 1_000, IsEnabledByDefault = true }],
            Habitats =
            [
                new MapResourceHabitat
                {
                    BiomeTypeId = BiomeType.Forest,
                    InitialQuantityMin = 200_000,
                    InitialQuantityMax = 400_000,
                    MaximumQuantity = 500_000,
                    GrowthPercentage = 0.1m,
                    SpreadLikelihood = 0.1m,
                    SpreadLimitMin = 2,
                    SpreadLimitMax = 5
                }
            ]
        };

        var scenario = new MapResourceDistributionScenario
        {
            ScenarioNumber = 1,
            Amounts = [new MapResourceDistributionScenarioAmount { AssetId = asset.Id, Amount = 10_000_000 }],
            BiomeWeights = [new MapResourceDistributionScenarioBiomeWeight { BiomeTypeId = BiomeType.Forest, Weight = 1 }]
        };

        var wood = new MapResourceType
        {
            Code = "TestWood",
            DisplayName = "Test Wood",
            Category = mapCategory,
            IsRenewable = true,
            IsDistribution = true,
            ExtractionMethod = ExtractionMethod.Fell,
            IsDiscoverableByDefault = true,
            DiscoveryLikelihood = 1m,
            EstimationFuzzyFactorMin = 0.1m,
            EstimationFuzzyFactorMax = 0.2m,
            SurvivalRounds = 5,
            ReseedLikelihood = 0.1m,
            Habitats = [new MapResourceHabitat { BiomeTypeId = BiomeType.Forest, MaximumQuantity = 20_000_000, GrowthPercentage = 0.05m }],
            DistributionScenarios = [scenario]
        };

        var coal = new MapResourceType
        {
            Code = "TestCoal",
            DisplayName = "Test Coal",
            Category = mapCategory,
            IsStratified = true,
            ExtractionMethod = ExtractionMethod.Mine,
            IsDiscoverableByDefault = true,
            DiscoveryLikelihood = 0.1m,
            Habitats = [new MapResourceHabitat { BiomeTypeId = BiomeType.Forest }],
            Layers =
            [
                new MapResourceTypeLayer
                {
                    LayerNumber = 1,
                    AssetId = asset.Id,
                    EmptyLikelihood = 0.25m,
                    QuantityMin = 1_000_000,
                    QuantityMax = 5_000_000,
                    EstimationFuzzyFactorMin = 0.1m,
                    EstimationFuzzyFactorMax = 0.2m
                }
            ]
        };

        context.MapResourceTypes.AddRange(berry, wood, coal);
        await context.SaveChangesAsync();

        return new MapResourceTestCatalogue(berry.Id, wood.Id, coal.Id, asset.Id, scenario.Id);
    }

    /// <summary>
    /// Adds one Asset per name, through the standalone path of the Asset hierarchy (a meta category and a category of its own), and saves.
    /// </summary>
    /// <returns>The Assets, in the order named.</returns>
    public static async Task<List<Asset>> SeedAssetsAsync(AscendantEmpireContext context, params string[] names)
    {
        var meta = new AssetMetaCategory { Code = "TestMeta", DisplayName = "Test Meta", SortOrder = 99, IconPath = "test.png" };
        context.AssetMetaCategories.Add(meta);
        await context.SaveChangesAsync();

        var category = new AssetCategory
        {
            AssetMetaCategoryId = meta.Id,
            Code = "TestMeta.TestCategory",
            CategoryType = AssetCategoryType.StandaloneAssets,
            DisplayName = "Test Category",
            SortOrder = 1
        };
        context.AssetCategories.Add(category);
        await context.SaveChangesAsync();

        // Sort orders are spaced by 10 from the caller's order, matching how the real catalogue is seeded.
        List<Asset> assets = [.. names.Select((name, index) => new Asset
        {
            Code = $"TestMeta.TestCategory.{name}",
            DisplayName = name,
            IconPath = "test.png",
            SortOrder = (index + 1) * 10,
            IsGradedAsset = false,
            AssetMetaCategoryId = meta.Id,
            AssetCategoryId = category.Id
        })];

        context.Assets.AddRange(assets);
        await context.SaveChangesAsync();

        return assets;
    }

    /// <summary>
    /// Adds a Biome row if the database does not have it. Biomes are seeded by the Entity Setup Tool, so the template database may not.
    /// </summary>
    public static async Task EnsureBiomeAsync(AscendantEmpireContext context, BiomeType biome)
    {
        if (await context.CampaignBiomes.AnyAsync(b => b.BiomeTypeId == biome) == false)
        {
            context.CampaignBiomes.Add(new CampaignBiome { BiomeTypeId = biome, Name = biome.ToString(), MovementCost = 1m, IsTraversable = true, MapEditorHtmlColor = "#228b22" });
            await context.SaveChangesAsync();
        }
    }

    /// <summary>
    /// Adds a Geography row if the database does not have it, for the same reason as <see cref="EnsureBiomeAsync"/>.
    /// </summary>
    public static async Task EnsureGeographyAsync(AscendantEmpireContext context, CampaignMapGeographyType geography)
    {
        if (await context.CampaignGeographies.AnyAsync(g => g.CampaignMapGeographyTypeId == geography) == false)
        {
            context.CampaignGeographies.Add(new CampaignGeography { CampaignMapGeographyTypeId = geography, Name = geography.ToString(), MovementCost = 1m, MapEditorIcon = null, IsPaintableOnCampaignMap = true });
            await context.SaveChangesAsync();
        }
    }
}
