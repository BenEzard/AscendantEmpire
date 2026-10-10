using AscendantEmpire.Application.Features.DatabaseSeeding.SeedData;
using AscendantEmpire.BackendContracts.MapResources.AddOrUpdateMapResourceType;
using AscendantEmpire.Domain.Enums.MapResources;
using AscendantEmpire.Domain.Enums.Maps;

namespace AscendantEmpire.Tests.Features.DatabaseSeeding;

/// <summary>
/// Tests for Timber in particular: that it is the Distribution the campaign-map design assumes, that its generated scenarios come out as the archetype and
/// band tables describe, and that bamboo and the other woods stay in their own Biomes.
/// </summary>
/// <remarks>
/// <para>
/// No database. What every seeded MapResource has to satisfy, whatever kind it is, is in <see cref="MapResourceTypeSeedDataTests"/> — this fixture only
/// covers what is true of Timber alone.
/// </para>
/// <para>
/// Scenarios are generated rather than listed, so these are the only place the generated set is held to the intent behind it. They would catch a share table
/// that no longer sums to 100, or a band quietly going missing from a Biome.
/// </para>
/// </remarks>
[TestFixture]
internal class TimberSeedDataTests
{
    /// <summary>
    /// The sizes a Forest or Woodland tile comes in. Kept apart from the jungle sizes because the two sets are offered to different Biomes, so filtering
    /// them together would credit a Biome with a size it is never offered.
    /// </summary>
    private static readonly int[] _BAMBOO_FREE_BAND_TOTALS = [3_000, 9_000, 16_000];

    /// <summary>
    /// The sizes a Jungle tile comes in.
    /// </summary>
    private static readonly int[] _JUNGLE_BAND_TOTALS = [6_000, 14_000, 24_000];

    /// <summary>
    /// Every size a tile can start at. Changing a band means changing one of these lists, which is the point: a scenario landing between them would mean the
    /// share arithmetic had drifted.
    /// </summary>
    private static readonly int[] _BAND_TOTALS = [.. _BAMBOO_FREE_BAND_TOTALS, .. _JUNGLE_BAND_TOTALS];

    private readonly TimberSeedData _timberSeed = new();

    private readonly Dictionary<string, int> _assetIdsByCode;

    private readonly AddOrUpdateMapResourceTypeDto _timber;

    public TimberSeedDataTests()
    {
        _assetIdsByCode = _timberSeed.AssetCodes
            .Select((code, index) => (Code: code, Id: index + 1))
            .ToDictionary(asset => asset.Code, asset => asset.Id);

        _timber = _timberSeed.Build(_assetIdsByCode);
    }

    [Test, Description("Timber is the renewable Distribution the rest of the design assumes, holding its Assets in scenarios rather than yields or layers.")]
    public void ShouldSeedTimberAsARenewableDistribution()
    {
        Assert.Multiple(() =>
        {
            Assert.That(_timber.Code, Is.EqualTo("Timber"));
            Assert.That(_timber.IsDistribution, Is.True);
            Assert.That(_timber.IsRenewable, Is.True, "A Distribution has to be renewable.");
            Assert.That(_timber.IsStratified, Is.False);
            Assert.That(_timber.ExtractionMethod, Is.EqualTo(ExtractionMethod.Fell));
            Assert.That(_timber.Yields, Is.Empty, "A Distribution holds its Assets in scenarios, so it must have no yields.");
            Assert.That(_timber.Layers, Is.Empty, "Only a stratified type has layers.");
            Assert.That(_timber.IconPath, Is.Null, "A tile covered in trees is already said by its Biome.");
        });
    }

    [Test, Description("Timber grows in the three wooded Biomes, one habitat each covering every Geography, with a ceiling and a growth rate and nothing else.")]
    public void ShouldSeedOneHabitatPerWoodedBiome()
    {
        Assert.Multiple(() =>
        {
            Assert.That(_timber.Habitats.Select(habitat => habitat.BiomeType), Is.EquivalentTo(new[] { BiomeType.Forest, BiomeType.Jungle, BiomeType.Woodland }));

            foreach (MapResourceHabitatDto habitat in _timber.Habitats)
            {
                Assert.That(habitat.GeographyType, Is.Null, $"The {habitat.BiomeType} habitat should cover every Geography.");
                Assert.That(habitat.MaximumQuantity, Is.Not.Null.And.GreaterThan(0), $"The {habitat.BiomeType} habitat needs a ceiling.");
                Assert.That(habitat.GrowthPercentage, Is.Not.Null.And.GreaterThan(0), $"The {habitat.BiomeType} habitat needs a growth rate.");
                Assert.That(habitat.InitialQuantityMin, Is.Null, "A Distribution starts at its scenario's amounts, not an initial range.");
                Assert.That(habitat.InitialQuantityMax, Is.Null, "A Distribution starts at its scenario's amounts, not an initial range.");
                Assert.That(habitat.SpreadLikelihood, Is.Null, "A Distribution never spreads.");
            }
        });
    }

    [Test, Description("Timber offers 45 mixes — ten bamboo-free archetypes and five jungle ones, each at three sizes — which is the count the step reports.")]
    public void ShouldGenerateAScenarioPerArchetypeAndBand()
    {
        Assert.Multiple(() =>
        {
            Assert.That(_timber.DistributionScenarios, Has.Count.EqualTo(45));
            Assert.That(_timberSeed.ItemCount, Is.EqualTo(_timber.DistributionScenarios.Count));
            Assert.That(_timber.DistributionScenarios.Select(scenario => scenario.DisplayName), Is.Unique, "Two scenarios would be indistinguishable in the editor's dropdown.");
        });
    }

    [Test, Description("A scenario's amounts sum to one of the quantity bands exactly, so the rounding of a share never loses or invents wood.")]
    public void ShouldMakeEveryScenarioSumToABandTotal()
    {
        Assert.Multiple(() =>
        {
            foreach (MapResourceDistributionScenarioDto scenario in _timber.DistributionScenarios)
            {
                int total = scenario.Amounts.Sum(amount => amount.Amount);

                Assert.That(_BAND_TOTALS, Does.Contain(total), $"Scenario {scenario.ScenarioNumber} ({scenario.DisplayName}) totals {total}, which is not a band total.");
            }
        });
    }

    [Test, Description("Bamboo only grows in jungle, and the other woods never do, which is the whole of that rule since a scenario's reach is its Biome weights.")]
    public void ShouldKeepBambooInJungleAndTheOtherWoodsOutOfIt()
    {
        int bambooId = _assetIdsByCode["ConstructionMaterials.Wood.Logs.Bamboo"];

        Assert.Multiple(() =>
        {
            foreach (MapResourceDistributionScenarioDto scenario in _timber.DistributionScenarios)
            {
                bool holdsBamboo = scenario.Amounts.Any(amount => amount.AssetId == bambooId);

                BiomeType[] biomes = [.. scenario.BiomeWeights.Select(weight => weight.BiomeType)];

                if (holdsBamboo)
                {
                    Assert.That(biomes, Is.EqualTo(new[] { BiomeType.Jungle }), $"Scenario {scenario.ScenarioNumber} ({scenario.DisplayName}) holds bamboo outside jungle.");
                }
                else
                {
                    Assert.That(biomes, Does.Not.Contain(BiomeType.Jungle), $"Scenario {scenario.ScenarioNumber} ({scenario.DisplayName}) has no bamboo but can appear in jungle.");
                }
            }
        });
    }

    [Test, Description("A woodland only gets the sizes it can hold: its 9,000 ceiling rules the dense band out, while a forest offers all three.")]
    public void ShouldWithholdTheDenseBandFromWoodland()
    {
        int woodlandCeiling = _timber.Habitats.Single(habitat => habitat.BiomeType == BiomeType.Woodland).MaximumQuantity!.Value;

        int[] woodlandTotals = [.. TotalsWeightedIn(BiomeType.Woodland).Distinct().Order()];
        int[] forestTotals = [.. TotalsWeightedIn(BiomeType.Forest).Distinct().Order()];

        Assert.Multiple(() =>
        {
            Assert.That(woodlandTotals, Is.EqualTo(_BAMBOO_FREE_BAND_TOTALS.Where(total => total <= woodlandCeiling).Order().ToArray()));
            Assert.That(forestTotals, Is.EqualTo(_BAMBOO_FREE_BAND_TOTALS.Order().ToArray()), "A forest should offer all three bamboo-free sizes.");
            Assert.That(woodlandTotals, Has.Length.LessThan(forestTotals.Length), "A woodland should offer fewer sizes than a forest.");
        });
    }

    [Test, Description("A rarer mix weighs less than a common one at the same size, so the rarity column really does decide how often a wood turns up.")]
    public void ShouldWeighARareMixBelowACommonOneAtTheSameSize()
    {
        int pureStand = WeightOf("Pure stand, established", BiomeType.Forest);
        int oakRich = WeightOf("Oak rich, established", BiomeType.Forest);
        int ebonyStand = WeightOf("Ebony stand, established", BiomeType.Forest);

        Assert.Multiple(() =>
        {
            Assert.That(pureStand, Is.GreaterThan(oakRich));
            Assert.That(oakRich, Is.GreaterThan(ebonyStand));
        });
    }

    /// <summary>
    /// The total of every scenario that can be rolled in one Biome.
    /// </summary>
    private IEnumerable<int> TotalsWeightedIn(BiomeType biomeType)
    {
        return _timber.DistributionScenarios
            .Where(scenario => scenario.BiomeWeights.Any(weight => weight.BiomeType == biomeType))
            .Select(scenario => scenario.Amounts.Sum(amount => amount.Amount));
    }

    /// <summary>
    /// One named scenario's weight in one Biome.
    /// </summary>
    private int WeightOf(string displayName, BiomeType biomeType)
    {
        MapResourceDistributionScenarioDto scenario = _timber.DistributionScenarios.Single(scenario => scenario.DisplayName == displayName);

        return scenario.BiomeWeights.Single(weight => weight.BiomeType == biomeType).Weight;
    }
}
