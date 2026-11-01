using AscendantEmpire.BackendContracts.MapResources.AddOrUpdateMapResourceType;
using AscendantEmpire.Domain.Enums.MapResources;
using AscendantEmpire.Domain.Enums.Maps;

namespace AscendantEmpire.Application.Features.DatabaseSeeding.SeedData;

/// <summary>
/// Seed data for the Berries asset.
/// </summary>
/// <remarks>
/// See <c>docs/design/campaign-map/map-resources.md</c>.
/// </remarks>
internal sealed class BerriesSeedData : IMapResourceTypeSeed
{
    /// <summary>
    /// The category Berries is listed under in the MapResource Type Manager, seeded by <see cref="MapResourceCategorySeedData"/>.
    /// </summary>
    private const string _FOOD_CATEGORY_CODE = "food";

    /// <summary>
    /// The one Asset a berry patch yields.
    /// </summary>
    private const string _BERRIES_ASSET = "Foodstuffs.PlantFoods.Berries";

    #region Habitats
    /// <summary>
    /// Where berries grow, and their values there, richest ceiling first.
    /// </summary>
    /// <remarks>
    /// <para>
    /// One row per Biome and Geography rather than per Biome. A Biome absent from this list is one berries cannot grow in at all.
    /// </para>
    /// <para>
    /// Quantities are logical units, scaled by <c>ScaledAmount.Scale</c> on the way into the database.
    /// </para>
    /// </remarks>
    private static readonly MapResourceHabitatDto[] _HABITATS =
    [
        new MapResourceHabitatDto
        {
            BiomeType = BiomeType.Grassland,
            GeographyType = CampaignMapGeographyType.Flat,
            InitialQuantityMin = 300,
            InitialQuantityMax = 2_000,
            MaximumQuantity = 5_000,
            GrowthPercentage = 0.15m,
            SpreadLikelihood = 0.20m,
            SpreadLimitMin = 1,
            SpreadLimitMax = 5
        },
        new MapResourceHabitatDto
        {
            BiomeType = BiomeType.Grassland,
            GeographyType = CampaignMapGeographyType.Hilly,
            InitialQuantityMin = 300,
            InitialQuantityMax = 1_500,
            MaximumQuantity = 5_000,
            GrowthPercentage = 0.15m,
            SpreadLikelihood = 0.20m,
            SpreadLimitMin = 1,
            SpreadLimitMax = 5
        },
        new MapResourceHabitatDto
        {
            BiomeType = BiomeType.Forest,
            GeographyType = CampaignMapGeographyType.Flat,
            InitialQuantityMin = 250,
            InitialQuantityMax = 1_000,
            MaximumQuantity = 4_000,
            GrowthPercentage = 0.10m,
            SpreadLikelihood = 0.10m,
            SpreadLimitMin = 1,
            SpreadLimitMax = 3
        },
        new MapResourceHabitatDto
        {
            BiomeType = BiomeType.Forest,
            GeographyType = CampaignMapGeographyType.Hilly,
            InitialQuantityMin = 250,
            InitialQuantityMax = 1_000,
            MaximumQuantity = 4_000,
            GrowthPercentage = 0.10m,
            SpreadLikelihood = 0.10m,
            SpreadLimitMin = 1,
            SpreadLimitMax = 3
        },
        new MapResourceHabitatDto
        {
            BiomeType = BiomeType.Woodland,
            GeographyType = CampaignMapGeographyType.Flat,
            InitialQuantityMin = 300,
            InitialQuantityMax = 1_200,
            MaximumQuantity = 4_000,
            GrowthPercentage = 0.10m,
            SpreadLikelihood = 0.10m,
            SpreadLimitMin = 1,
            SpreadLimitMax = 4
        },
        new MapResourceHabitatDto
        {
            BiomeType = BiomeType.Woodland,
            GeographyType = CampaignMapGeographyType.Hilly,
            InitialQuantityMin = 300,
            InitialQuantityMax = 1_200,
            MaximumQuantity = 4_000,
            GrowthPercentage = 0.10m,
            SpreadLikelihood = 0.10m,
            SpreadLimitMin = 1,
            SpreadLimitMax = 4
        },
        new MapResourceHabitatDto
        {
            BiomeType = BiomeType.Arid,
            GeographyType = CampaignMapGeographyType.Flat,
            InitialQuantityMin = 10,
            InitialQuantityMax = 50,
            MaximumQuantity = 50,
            GrowthPercentage = 0.05m
        },
        new MapResourceHabitatDto
        {
            BiomeType = BiomeType.Arid,
            GeographyType = CampaignMapGeographyType.Hilly,
            InitialQuantityMin = 10,
            InitialQuantityMax = 50,
            MaximumQuantity = 50,
            GrowthPercentage = 0.05m,
            SpreadLikelihood = 0.02m,
            SpreadLimitMin = 1,
            SpreadLimitMax = 2
        }
    ];
    #endregion

    /// <summary>
    /// Lower case, as the type was first authored in the MapResource Type Manager.
    /// </summary>
    public string Code => "berries";

    public IReadOnlyCollection<string> AssetCodes => [_BERRIES_ASSET];

    public int ItemCount => 1;

    public AddOrUpdateMapResourceTypeDto Build(IReadOnlyDictionary<string, int> assetIdsByCode)
    {
        return new AddOrUpdateMapResourceTypeDto
        {
            Code = Code,
            DisplayName = "Berries",
            MapResourceCategoryCode = _FOOD_CATEGORY_CODE,

            IconPath = "/resources/food/berries_40px.png",

            IsRenewable = true,
            IsStratified = false,
            IsDistribution = false,

            IsWaterResource = false,

            ExtractionMethod = ExtractionMethod.Gather,
            CaptureLikelihood = null,
            RequiresResearchToExtract = false,

            IsDiscoverableByDefault = true,
            DiscoveryLikelihood = 1.0m,
            EstimationFuzzyFactorMin = 0.05m,
            EstimationFuzzyFactorMax = 0.20m,

            // A patch stripped bare lies dormant for three Rounds, with a one-in-five chance each Round of coming back.
            SurvivalRounds = 3,
            ReseedLikelihood = 0.20m,

            Habitats = [.. _HABITATS],

            Yields =
            [
                new MapResourceTypeYieldDto
                {
                    AssetId = assetIdsByCode[_BERRIES_ASSET],
                    AmountPerUnit = 1,
                    IsEnabledByDefault = true
                }
            ]
        };
    }
}
