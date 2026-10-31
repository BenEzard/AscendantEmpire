using AscendantEmpire.BackendContracts.MapResources.AddOrUpdateMapResourceType;
using AscendantEmpire.Domain.Enums.MapResources;
using AscendantEmpire.Domain.Enums.Maps;

namespace AscendantEmpire.Application.Features.DatabaseSeeding.SeedData;

/// <summary>
/// Berries: the forageable fruit of a Grassland, Forest, Woodland or Arid tile, held as a single yield of the Berries Asset.
/// </summary>
/// <remarks>
/// <para>
/// A plain renewable type rather than a Distribution: a berry patch is one thing in one quantity, so it needs no composition and holds its Asset in a yield.
/// Everything that varies by tile is in the habitats - how much a patch starts with, how big it can get, how fast it grows back, and how readily it spreads
/// onto a neighbour.
/// </para>
/// <para>
/// Berries are one of the Assets a new game's founding provision is sized from, so the Asset this yields has to exist whether or not a map has any berry
/// patch on it. That Asset is seeded by <see cref="AssetSeedData"/>; this file only points at it.
/// </para>
/// <para>
/// See <c>docs/design/campaign-map/map-resources.md</c>.
/// </para>
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
    /// <remarks>
    /// This is the code the Asset upsert derives from the category code and the Asset's DisplayName, so it has to stay in step with
    /// <see cref="AssetSeedData"/> - <c>MapResourceTypeSeedDataTests</c> checks it against the Asset seed rather than trusting this constant.
    /// </remarks>
    private const string _BERRIES_ASSET = "Foodstuffs.PlantFoods.Berries";

    #region Habitats
    /// <summary>
    /// Where berries grow, and their values there, richest ceiling first.
    /// </summary>
    /// <remarks>
    /// <para>
    /// One row per Biome and Geography rather than per Biome, because a hillside holds a different starting patch from the flat ground below it even where
    /// everything else about the two is the same. A Biome absent from this list is one berries cannot grow in at all - there is no Desert, Tundra, Ice or
    /// Coastal row, so a patch can neither start nor spread onto those tiles.
    /// </para>
    /// <para>
    /// Quantities are logical units, scaled by <c>ScaledAmount.Scale</c> on the way into the database. Hedgerow grassland is the best berry country and the
    /// fastest to spread; an arid tile holds a token patch two orders of magnitude smaller, which is why its ceiling is also its largest starting size.
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
            // The same ceiling, growth and spread as flat grassland, but a smaller starting patch: a slope carries less hedgerow.
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
            // A forest's two Geographies differ in nothing: under a closed canopy the slope makes no odds to the fruit.
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
            // A woodland's open canopy starts a larger patch than a forest's and passes it one tile further, on the same ceiling.
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
            // Flat arid ground grows berries but never passes them on. The three spread values are deliberately all empty, which is how a renewable type is
            // set never to spread out of one habitat; the hilly arid row below is the only arid tile that spreads, and barely.
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
    /// <remarks>
    /// The catalogue upsert matches on code case-insensitively, so the casing here is only what the row is stored under, not what it is found by.
    /// </remarks>
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

            // No icon: the map editor falls back to the category's own marker, which is all a berry patch needs.
            IconPath = null,

            // Renewable and not a Distribution, which is the one kind that can spread onto a neighbouring tile.
            IsRenewable = true,
            IsStratified = false,
            IsDistribution = false,

            ExtractionMethod = ExtractionMethod.Gather,
            CaptureLikelihood = null,
            RequiresResearchToExtract = false,

            // A patch in fruit is unmistakable, so discovery is certain. How much is on the tile is not, which is what the fuzzy factor range is for - wider
            // than Timber's, because a patch's size is harder to eye than a stand of trees.
            IsDiscoverableByDefault = true,
            DiscoveryLikelihood = 1.0m,
            EstimationFuzzyFactorMin = 0.05m,
            EstimationFuzzyFactorMax = 0.20m,

            // A patch stripped bare lies dormant for three Rounds, with a one-in-five chance each Round of coming back.
            SurvivalRounds = 3,
            ReseedLikelihood = 0.20m,

            Habitats = [.. _HABITATS],

            // One unit gathered gives one unit of berries, and the yield is on from the start: there is nothing to unlock about picking fruit.
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
