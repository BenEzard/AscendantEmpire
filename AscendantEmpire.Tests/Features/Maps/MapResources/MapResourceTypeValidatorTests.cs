using AscendantEmpire.Application.Common.Validation;
using AscendantEmpire.Application.Features.Maps.MapResources.Validators;
using AscendantEmpire.BackendContracts.MapResources.AddOrUpdateMapResourceType;
using AscendantEmpire.Domain.Enums.Maps;
using AscendantEmpire.Domain.Enums.MapResources;

namespace AscendantEmpire.Tests.Features.Maps.MapResources;

/// <summary>
/// Tests for <see cref="MapResourceTypeValidator"/>: that one well-formed type of each kind passes, and that each rule tying a value or child to the type's kind rejects a
/// type that breaks it.
/// </summary>
/// <remarks>
/// No database - the validator is pure. Each rejection test starts from a valid type of the right kind and breaks exactly one thing, so a failure points at one rule.
/// </remarks>
[TestFixture]
internal class MapResourceTypeValidatorTests
{
    // Asset ids only need to be distinct: whether they exist is the handler's check, not the validator's.
    private const int _FORAGE = 1;
    private const int _MEDIUM_GAME = 2;
    private const int _PELT = 3;
    private const int _HORSE = 4;
    private const int _OAK = 5;
    private const int _GENERIC_WOOD = 6;
    private const int _CEDAR = 7;
    private const int _LOW_COAL = 8;
    private const int _HIGH_COAL = 9;
    private const int _COPPER = 10;

    private readonly MapResourceTypeValidator _validator = new();

    /// <summary>
    /// One valid type of each kind, named for the test runner.
    /// </summary>
    private static IEnumerable<TestCaseData> ValidTypes()
    {
        yield return new TestCaseData(Berry()).SetName("A renewable type that spreads (Berry) is valid.");
        yield return new TestCaseData(Deer()).SetName("A renewable type with two yields, one off by default (Deer), is valid.");
        yield return new TestCaseData(Horse()).SetName("A Capture type with a CaptureLikelihood (Horse) is valid.");
        yield return new TestCaseData(ForestWood()).SetName("A Distribution (Forest Wood) is valid.");
        yield return new TestCaseData(CoalSeam()).SetName("A stratified type (Coal Seam) is valid.");
        yield return new TestCaseData(CopperDeposit()).SetName("A type that is neither renewable nor stratified (Copper Deposit) is valid.");
    }

    [TestCaseSource(nameof(ValidTypes))]
    public void ShouldAcceptAWellFormedTypeOfEachKind(AddOrUpdateMapResourceTypeDto type)
    {
        ValidationResult result = _validator.Validate(type);

        Assert.That(result.IsValid, Is.True, string.Join("; ", result.Errors.Select(e => e.ErrorMessage)));
    }

    #region Kind
    [Test, Description("Renewable and stratified are mutually exclusive.")]
    public void ShouldRejectATypeThatIsBothRenewableAndStratified()
    {
        AssertRejected(CoalSeam() with { IsRenewable = true }, "cannot be both IsRenewable and IsStratified");
    }

    [Test, Description("A Distribution grows, so it must be renewable.")]
    public void ShouldRejectADistributionThatIsNotRenewable()
    {
        AssertRejected(ForestWood() with { IsRenewable = false }, "must also be IsRenewable");
    }
    #endregion

    #region Extraction
    [Test, Description("A Capture can fail, so it needs a chance of succeeding.")]
    public void ShouldRejectACaptureWithoutACaptureLikelihood()
    {
        AssertRejected(Horse() with { CaptureLikelihood = null }, "CaptureLikelihood must be set when, and only when");
    }

    [Test, Description("Only a Capture can fail, so any other method must leave CaptureLikelihood empty.")]
    public void ShouldRejectACaptureLikelihoodOnAnotherMethod()
    {
        AssertRejected(Deer() with { CaptureLikelihood = 0.5m }, "CaptureLikelihood must be set when, and only when");
    }

    [TestCase(-0.1)]
    [TestCase(1.001)]
    public void ShouldRejectADiscoveryLikelihoodOutsideZeroToOne(decimal likelihood)
    {
        AssertRejected(Berry() with { DiscoveryLikelihood = likelihood }, "DiscoveryLikelihood must be between 0 and 1");
    }
    #endregion

    #region Exhaustion and estimation
    [Test, Description("A renewable type goes dormant when exhausted, so it must say how long it survives and how likely it is to come back.")]
    public void ShouldRejectARenewableTypeWithoutSurvivalValues()
    {
        AssertRejected(Berry() with { SurvivalRounds = null }, "must both be set for a renewable MapResourceType");
    }

    [Test, Description("A type that is not renewable never goes dormant, so it must leave the survival values empty.")]
    public void ShouldRejectSurvivalValuesOnATypeThatIsNotRenewable()
    {
        AssertRejected(CopperDeposit() with { SurvivalRounds = 5, ReseedLikelihood = 0.2m }, "both be empty otherwise");
    }

    [Test, Description("A stratified type judges each layer separately, so the type itself has no fuzzy factor range.")]
    public void ShouldRejectAFuzzyFactorRangeOnAStratifiedType()
    {
        AssertRejected(CoalSeam() with { EstimationFuzzyFactorMin = 0.1m, EstimationFuzzyFactorMax = 0.2m }, "sets its fuzzy factor range on each layer");
    }

    [Test, Description("Every other kind rolls its instances' fuzzy factor from its own range.")]
    public void ShouldRejectAnInvertedFuzzyFactorRange()
    {
        AssertRejected(Berry() with { EstimationFuzzyFactorMin = 0.3m, EstimationFuzzyFactorMax = 0.1m }, "EstimationFuzzyFactorMax are required");
    }
    #endregion

    #region Children by kind
    [Test, Description("A Distribution holds its Assets in scenarios, never in yields.")]
    public void ShouldRejectYieldsOnADistribution()
    {
        AssertRejected(ForestWood() with { Yields = [Yield(_OAK)] }, "a Distribution cannot have any yields");
    }

    [Test, Description("A stratified type holds its Assets in layers, and must have at least one.")]
    public void ShouldRejectAStratifiedTypeWithoutLayers()
    {
        AssertRejected(CoalSeam() with { Layers = [] }, "needs at least one layer");
    }

    [Test, Description("A single-quantity type gives its Assets through yields, and must have at least one.")]
    public void ShouldRejectASingleQuantityTypeWithoutYields()
    {
        AssertRejected(Berry() with { Yields = [] }, "needs at least one yield");
    }

    [Test, Description("A type with no habitat could never exist anywhere.")]
    public void ShouldRejectATypeWithoutHabitats()
    {
        AssertRejected(Berry() with { Habitats = [] }, "at least one habitat is required");
    }

    [Test, Description("Each Asset appears at most once in a type's yields.")]
    public void ShouldRejectADuplicatedYield()
    {
        AssertRejected(Deer() with { Yields = [Yield(_MEDIUM_GAME), Yield(_MEDIUM_GAME)] }, "appears more than once in the yields");
    }
    #endregion

    #region Habitats
    [Test, Description("Two Biome-wide habitats for the same Biome would leave the tile's values ambiguous.")]
    public void ShouldRejectTwoBiomeWideHabitatsForOneBiome()
    {
        AssertRejected(Berry() with { Habitats = [BerryHabitat(BiomeType.Forest), BerryHabitat(BiomeType.Forest)] }, "more than one habitat for Forest");
    }

    [Test, Description("A Distribution never spreads, so its habitats have no spread values.")]
    public void ShouldRejectSpreadValuesOnADistributionHabitat()
    {
        MapResourceHabitatDto spreading = ForestWoodHabitat() with { SpreadLikelihood = 0.1m, SpreadLimitMin = 1, SpreadLimitMax = 3 };

        AssertRejected(ForestWood() with { Habitats = [spreading] }, "only a renewable MapResourceType that is not a Distribution can spread");
    }

    [Test, Description("A SpreadLikelihood with no SpreadLimit range would spread without limit, so the three are set together or not at all.")]
    public void ShouldRejectASpreadLikelihoodWithoutASpreadLimitRange()
    {
        MapResourceHabitatDto partial = BerryHabitat(BiomeType.Forest) with { SpreadLimitMin = null, SpreadLimitMax = null };

        AssertRejected(Berry() with { Habitats = [partial] }, "must be set together");
    }

    [Test, Description("A renewable type may be set never to spread in a habitat by leaving all three spread values empty.")]
    public void ShouldAcceptARenewableHabitatWithNoSpreadValues()
    {
        MapResourceHabitatDto noSpread = BerryHabitat(BiomeType.Forest) with { SpreadLikelihood = null, SpreadLimitMin = null, SpreadLimitMax = null };

        ValidationResult result = _validator.Validate(Berry() with { Habitats = [noSpread] });

        Assert.That(result.IsValid, Is.True, string.Join("; ", result.Errors.Select(e => e.ErrorMessage)));
    }

    [Test, Description("An instance could otherwise start above the ceiling it grows to.")]
    public void ShouldRejectAnInitialQuantityAboveTheMaximum()
    {
        MapResourceHabitatDto tooFull = BerryHabitat(BiomeType.Forest) with { InitialQuantityMax = 600, MaximumQuantity = 500 };

        AssertRejected(Berry() with { Habitats = [tooFull] }, "InitialQuantityMax cannot be above MaximumQuantity");
    }

    [Test, Description("Only a renewable type grows, so any other must leave MaximumQuantity and GrowthPercentage empty.")]
    public void ShouldRejectGrowthValuesOnATypeThatIsNotRenewable()
    {
        MapResourceHabitatDto growing = CopperHabitat() with { MaximumQuantity = 1_000, GrowthPercentage = 0.1m };

        AssertRejected(CopperDeposit() with { Habitats = [growing] }, "only a renewable MapResourceType grows");
    }

    [Test, Description("A Distribution starts at its scenario's amounts, so its habitats have no initial quantity range.")]
    public void ShouldRejectAnInitialQuantityRangeOnADistributionHabitat()
    {
        MapResourceHabitatDto withInitial = ForestWoodHabitat() with { InitialQuantityMin = 100, InitialQuantityMax = 200 };

        AssertRejected(ForestWood() with { Habitats = [withInitial] }, "does not start from an initial quantity");
    }
    #endregion

    #region Scenarios and layers
    [Test, Description("A scenario with no Assets would create an instance with nothing to extract.")]
    public void ShouldRejectAScenarioWithNoAmounts()
    {
        MapResourceDistributionScenarioDto empty = OakRichScenario() with { Amounts = [] };

        AssertRejected(ForestWood() with { DistributionScenarios = [empty] }, "at least one Asset amount is required");
    }

    [Test, Description("A Biome where the scenario should never be picked has no weight rather than a weight of 0.")]
    public void ShouldRejectAZeroScenarioWeight()
    {
        MapResourceDistributionScenarioDto zeroWeight = OakRichScenario() with
        {
            BiomeWeights = [new MapResourceDistributionScenarioBiomeWeightDto { BiomeType = BiomeType.Forest, Weight = 0 }]
        };

        AssertRejected(ForestWood() with { DistributionScenarios = [zeroWeight] }, "the weight in Forest must be more than 0");
    }

    [Test, Description("Layers are reached in order from the top, so they are numbered 1, 2, 3... with no gaps.")]
    public void ShouldRejectLayersWithAGap()
    {
        AssertRejected(CoalSeam() with { Layers = [CoalLayer(1), CoalLayer(3)] }, "numbered 1, 2, 3... with no gaps or repeats");
    }

    [Test, Description("The first layer is found through the type's own DiscoveryLikelihood and needs no mine.")]
    public void ShouldRejectGatesOnTheFirstLayer()
    {
        AssertRejected(CoalSeam() with { Layers = [CoalLayer(1) with { DiscoveryLikelihood = 0.5m }] }, "the first layer uses the type's DiscoveryLikelihood");
    }

    [Test, Description("Every layer below the first has its own discovery likelihood.")]
    public void ShouldRejectADeeperLayerWithoutADiscoveryLikelihood()
    {
        AssertRejected(CoalSeam() with { Layers = [CoalLayer(1), CoalLayer(2) with { DiscoveryLikelihood = null }] }, "DiscoveryLikelihood is required below the first layer");
    }
    #endregion

    /// <summary>
    /// Asserts the type is rejected with a message containing <paramref name="expected"/>.
    /// </summary>
    private void AssertRejected(AddOrUpdateMapResourceTypeDto type, string expected)
    {
        ValidationResult result = _validator.Validate(type);

        Assert.That(result.Errors.Select(e => e.ErrorMessage), Has.Some.Contains(expected));
    }

    #region Valid types
    /// <summary>
    /// Renewable, spreads, one yield. A Biome-wide Forest habitat and a Hilly Forest one, which may coexist.
    /// </summary>
    private static AddOrUpdateMapResourceTypeDto Berry()
    {
        return RenewableBase("Berry", ExtractionMethod.Gather) with
        {
            RequiresResearchToExtract = true,
            Yields = [Yield(_FORAGE)],
            Habitats = [BerryHabitat(BiomeType.Forest), BerryHabitat(BiomeType.Forest) with { GeographyType = CampaignMapGeographyType.Hilly }],
        };
    }

    /// <summary>
    /// Renewable, two yields, the pelt off until hide-working.
    /// </summary>
    private static AddOrUpdateMapResourceTypeDto Deer()
    {
        return RenewableBase("Deer", ExtractionMethod.Hunt) with
        {
            Yields = [Yield(_MEDIUM_GAME), Yield(_PELT) with { IsEnabledByDefault = false }],
            Habitats = [BerryHabitat(BiomeType.Woodland)],
        };
    }

    /// <summary>
    /// Renewable, captured rather than hunted.
    /// </summary>
    private static AddOrUpdateMapResourceTypeDto Horse()
    {
        return RenewableBase("Horse", ExtractionMethod.Capture) with
        {
            CaptureLikelihood = 0.4m,
            Yields = [Yield(_HORSE)],
            Habitats = [BerryHabitat(BiomeType.Grassland)],
        };
    }

    /// <summary>
    /// A Distribution: renewable, no yields, one scenario, no initial quantity and no spread.
    /// </summary>
    private static AddOrUpdateMapResourceTypeDto ForestWood()
    {
        return RenewableBase("ForestWood", ExtractionMethod.Fell) with
        {
            IsDistribution = true,
            IconPath = null,
            Habitats = [ForestWoodHabitat()],
            DistributionScenarios = [OakRichScenario()],
        };
    }

    /// <summary>
    /// Stratified: two layers holding different Assets, no fuzzy range of its own, and a habitat with no values.
    /// </summary>
    private static AddOrUpdateMapResourceTypeDto CoalSeam()
    {
        return new AddOrUpdateMapResourceTypeDto
        {
            Code = "CoalSeam",
            DisplayName = "Coal Seam",
            MapResourceCategoryCode = "Food",
            IsRenewable = false,
            IsStratified = true,
            IsDistribution = false,
            ExtractionMethod = ExtractionMethod.Mine,
            RequiresResearchToExtract = false,
            IsDiscoverableByDefault = true,
            DiscoveryLikelihood = 0.1m,
            Habitats = [new MapResourceHabitatDto { BiomeType = BiomeType.Tundra, GeographyType = CampaignMapGeographyType.Hilly }],
            Layers = [CoalLayer(1), CoalLayer(2) with { AssetId = _HIGH_COAL }],
        };
    }

    /// <summary>
    /// Neither renewable nor stratified: a fixed amount with one yield.
    /// </summary>
    private static AddOrUpdateMapResourceTypeDto CopperDeposit()
    {
        return new AddOrUpdateMapResourceTypeDto
        {
            Code = "CopperDeposit",
            DisplayName = "Copper Deposit",
            MapResourceCategoryCode = "Metals",
            IsRenewable = false,
            IsStratified = false,
            IsDistribution = false,
            ExtractionMethod = ExtractionMethod.Mine,
            RequiresResearchToExtract = true,
            IsDiscoverableByDefault = true,
            DiscoveryLikelihood = 0.1m,
            EstimationFuzzyFactorMin = 0.2m,
            EstimationFuzzyFactorMax = 0.4m,
            Yields = [Yield(_COPPER)],
            Habitats = [CopperHabitat()],
        };
    }

    /// <summary>
    /// The values every renewable type here shares. The caller adds its yields or scenarios, and its habitats.
    /// </summary>
    private static AddOrUpdateMapResourceTypeDto RenewableBase(string code, ExtractionMethod method)
    {
        return new AddOrUpdateMapResourceTypeDto
        {
            Code = code,
            DisplayName = code,
            MapResourceCategoryCode = "Food",
            IconPath = $"/icons/map-assets/{code}.png",
            IsRenewable = true,
            IsStratified = false,
            IsDistribution = false,
            ExtractionMethod = method,
            RequiresResearchToExtract = false,
            IsDiscoverableByDefault = true,
            DiscoveryLikelihood = 1m,
            EstimationFuzzyFactorMin = 0.1m,
            EstimationFuzzyFactorMax = 0.3m,
            SurvivalRounds = 5,
            ReseedLikelihood = 0.2m,
        };
    }

    private static MapResourceTypeYieldDto Yield(int assetId)
    {
        return new MapResourceTypeYieldDto { AssetId = assetId, AmountPerUnit = 1, IsEnabledByDefault = true };
    }

    /// <summary>
    /// A renewable, spreading habitat: initial quantity, ceiling, growth and spread values all set.
    /// </summary>
    private static MapResourceHabitatDto BerryHabitat(BiomeType biome)
    {
        return new MapResourceHabitatDto
        {
            BiomeType = biome,
            InitialQuantityMin = 200,
            InitialQuantityMax = 400,
            MaximumQuantity = 500,
            GrowthPercentage = 0.1m,
            SpreadLikelihood = 0.1m,
            SpreadLimitMin = 2,
            SpreadLimitMax = 5,
        };
    }

    /// <summary>
    /// A Distribution habitat: a ceiling on the total and a growth percentage, and nothing else.
    /// </summary>
    private static MapResourceHabitatDto ForestWoodHabitat()
    {
        return new MapResourceHabitatDto { BiomeType = BiomeType.Forest, MaximumQuantity = 20_000, GrowthPercentage = 0.05m };
    }

    /// <summary>
    /// A habitat for a type that neither grows nor spreads: only an initial quantity range.
    /// </summary>
    private static MapResourceHabitatDto CopperHabitat()
    {
        return new MapResourceHabitatDto { BiomeType = BiomeType.Arid, GeographyType = CampaignMapGeographyType.Mountainous, InitialQuantityMin = 1_000, InitialQuantityMax = 3_000 };
    }

    /// <summary>
    /// The spec's Forest Wood scenario 1: oak 200, generic 10,000, cedar 30.
    /// </summary>
    private static MapResourceDistributionScenarioDto OakRichScenario()
    {
        return new MapResourceDistributionScenarioDto
        {
            ScenarioNumber = 1,
            Amounts =
            [
                new MapResourceDistributionScenarioAmountDto { AssetId = _OAK, Amount = 200 },
                new MapResourceDistributionScenarioAmountDto { AssetId = _GENERIC_WOOD, Amount = 10_000 },
                new MapResourceDistributionScenarioAmountDto { AssetId = _CEDAR, Amount = 30 },
            ],
            BiomeWeights = [new MapResourceDistributionScenarioBiomeWeightDto { BiomeType = BiomeType.Forest, Weight = 3 }],
        };
    }

    /// <summary>
    /// A low-grade coal layer. Layer 1 carries no gates; any deeper layer carries a discovery likelihood and a mine level.
    /// </summary>
    private static MapResourceTypeLayerDto CoalLayer(int layerNumber)
    {
        return new MapResourceTypeLayerDto
        {
            LayerNumber = layerNumber,
            AssetId = _LOW_COAL,
            EmptyLikelihood = 0.25m,
            QuantityMin = 1_000,
            QuantityMax = 5_000,
            DiscoveryLikelihood = layerNumber == 1 ? null : 0.3m,
            RequiredMineLevel = layerNumber == 1 ? null : layerNumber,
            EstimationFuzzyFactorMin = 0.1m * layerNumber,
            EstimationFuzzyFactorMax = 0.2m * layerNumber,
        };
    }
    #endregion
}
