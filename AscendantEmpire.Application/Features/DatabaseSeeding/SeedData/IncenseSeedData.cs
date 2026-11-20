using AscendantEmpire.BackendContracts.MapResources.AddOrUpdateMapResourceType;
using AscendantEmpire.Domain.Enums.MapResources;
using AscendantEmpire.Domain.Enums.Maps;

namespace AscendantEmpire.Application.Features.DatabaseSeeding.SeedData;

/// <summary>
/// Seed data for the Incense asset.
/// </summary>
/// <remarks>
/// See <c>docs/design/campaign-map/map-resources.md</c>.
/// </remarks>
internal sealed class IncenseSeedData : IMapResourceTypeSeed
{
    /// <summary>
    /// The category Incense is listed under in the MapResource Type Manager, seeded by <see cref="MapResourceCategorySeedData"/>.
    /// </summary>
    private const string _LUXURIES_CATEGORY_CODE = "luxuries";

    /// <summary>
    /// The one Asset an incense stand yields.
    /// </summary>
    private const string _INCENSE_ASSET = "RawMaterials.RawMaterials.Incense";

    #region Habitats
    /// <summary>
    /// Where incense grows, and its values there, richest first.
    /// </summary>
    /// <remarks>
    /// <para>
    /// One row per Biome and Geography rather than per Biome. A Biome absent from this list is one incense cannot grow in at all, and naming a Geography on
    /// every row is what keeps the stands off mountains.
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
            InitialQuantityMin = 500,
            InitialQuantityMax = 4_000,
            MaximumQuantity = 5_000,
            GrowthPercentage = 0.10m,
            SpreadLikelihood = 0.05m,
            SpreadLimitMin = 1,
            SpreadLimitMax = 5
        },
        new MapResourceHabitatDto
        {
            BiomeType = BiomeType.Grassland,
            GeographyType = CampaignMapGeographyType.Hilly,
            InitialQuantityMin = 500,
            InitialQuantityMax = 4_000,
            MaximumQuantity = 5_000,
            GrowthPercentage = 0.10m,
            SpreadLikelihood = 0.05m,
            SpreadLimitMin = 1,
            SpreadLimitMax = 5
        },
        new MapResourceHabitatDto
        {
            BiomeType = BiomeType.Woodland,
            GeographyType = CampaignMapGeographyType.Flat,
            InitialQuantityMin = 500,
            InitialQuantityMax = 4_000,
            MaximumQuantity = 5_000,
            GrowthPercentage = 0.10m,
            SpreadLikelihood = 0.05m,
            SpreadLimitMin = 1,
            SpreadLimitMax = 3
        },
        new MapResourceHabitatDto
        {
            BiomeType = BiomeType.Woodland,
            GeographyType = CampaignMapGeographyType.Hilly,
            InitialQuantityMin = 500,
            InitialQuantityMax = 4_000,
            MaximumQuantity = 5_000,
            GrowthPercentage = 0.10m,
            SpreadLikelihood = 0.05m,
            SpreadLimitMin = 1,
            SpreadLimitMax = 3
        },
        new MapResourceHabitatDto
        {
            BiomeType = BiomeType.Arid,
            GeographyType = CampaignMapGeographyType.Flat,
            InitialQuantityMin = 500,
            InitialQuantityMax = 1_500,
            MaximumQuantity = 5_000,
            GrowthPercentage = 0.05m,
            SpreadLikelihood = 0.02m,
            SpreadLimitMin = 1,
            SpreadLimitMax = 1
        },
        new MapResourceHabitatDto
        {
            BiomeType = BiomeType.Arid,
            GeographyType = CampaignMapGeographyType.Hilly,
            InitialQuantityMin = 500,
            InitialQuantityMax = 1_500,
            MaximumQuantity = 5_000,
            GrowthPercentage = 0.05m,
            SpreadLikelihood = 0.02m,
            SpreadLimitMin = 1,
            SpreadLimitMax = 1
        }
    ];
    #endregion

    /// <summary>
    /// Lower case, as the type was first authored in the MapResource Type Manager.
    /// </summary>
    public string Code => "incense";

    public IReadOnlyCollection<string> AssetCodes => [_INCENSE_ASSET];

    public int ItemCount => 1;

    public AddOrUpdateMapResourceTypeDto Build(IReadOnlyDictionary<string, int> assetIdsByCode)
    {
        return new AddOrUpdateMapResourceTypeDto
        {
            Code = Code,
            DisplayName = "Incense",
            MapResourceCategoryCode = _LUXURIES_CATEGORY_CODE,

            IconPath = "/resources/luxuries/incense_40px.png",

            IsRenewable = true,
            IsStratified = false,
            IsDistribution = false,

            IsWaterResource = false,

            ExtractionMethod = ExtractionMethod.Gather,
            CaptureLikelihood = null,
            RequiresResearchToExtract = false,

            IsDiscoverableByDefault = true,
            DiscoveryLikelihood = 1.0m,
            EstimationFuzzyFactorMin = 0.10m,
            EstimationFuzzyFactorMax = 0.20m,

            // A stand stripped bare lies dormant for three Rounds, and comes back far less readily than a berry patch.
            SurvivalRounds = 3,
            ReseedLikelihood = 0.05m,

            Habitats = [.. _HABITATS],

            Yields =
            [
                new MapResourceTypeYieldDto
                {
                    AssetId = assetIdsByCode[_INCENSE_ASSET],
                    AmountPerUnit = 1,
                    IsEnabledByDefault = true
                }
            ]
        };
    }
}
