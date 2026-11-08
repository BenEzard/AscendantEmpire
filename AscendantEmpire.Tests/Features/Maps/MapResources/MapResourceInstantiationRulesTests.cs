using AscendantEmpire.Application.Features.Maps.MapResources.Classes;
using AscendantEmpire.Application.Features.Maps.MapResources.Services;
using AscendantEmpire.Domain.Enums.Maps;

namespace AscendantEmpire.Tests.Features.Maps.MapResources;

/// <summary>
/// Pins the rules that turn a map's MapResource placements into the instances one Game starts with.
/// </summary>
/// <remarks>
/// No database: <see cref="MapResourceInstantiationRules"/> is pure so that every roll can be pinned directly, which is the only way to prove a Game is
/// reproducible from its seed. The service that reads the placements and writes the rows is pinned separately.
/// </remarks>
[TestFixture]
internal sealed class MapResourceInstantiationRulesTests
{
    private const long _SEED = 20261107;

    /// <summary>
    /// The Game's block of tile ids starts here, so a tile's id is this plus its row-major position.
    /// </summary>
    private const int _OFFSET = 1000;

    private const int _MAP_WIDTH = 10;

    private const int _BERRY_TYPE_ID = 11;

    private const int _TILE_X = 4;
    private const int _TILE_Y = 5;

    /// <summary>
    /// The id the tile at (4, 5) must get on a 10-wide map whose block starts at 1,000.
    /// </summary>
    private const int _EXPECTED_TILE_ID = _OFFSET + _TILE_X + (_TILE_Y * _MAP_WIDTH);

    private const long _QUANTITY_MIN = 1_000;
    private const long _QUANTITY_MAX = 9_000;

    private const int _SPREAD_LIMIT_MIN = 2;
    private const int _SPREAD_LIMIT_MAX = 5;

    private const decimal _FUZZY_MIN = 0.050m;
    private const decimal _FUZZY_MAX = 0.150m;

    private const int _WOOD_TYPE_ID = 7;

    private const int _COMMON_SCENARIO_ID = 301;
    private const int _RARE_SCENARIO_ID = 302;
    private const int _JUNGLE_SCENARIO_ID = 303;

    private const int _COMMON_ASSET_ID = 41;
    private const int _GENERIC_ASSET_ID = 42;
    private const int _RARE_ASSET_ID = 45;

    private const long _COMMON_ASSET_AMOUNT = 200_000;

    /// <summary>
    /// How many tiles the weighting tests draw over. Large enough that a 20-to-1 weight cannot plausibly invert.
    /// </summary>
    private const int _WEIGHT_SAMPLE_TILES = 400;

    private const int _COAL_TYPE_ID = 13;

    /// <summary>
    /// Three layers, so a per-layer roll can be told apart from one value copied down the stack.
    /// </summary>
    private const int _COAL_LAYER_COUNT = 3;

    private const long _LAYER_QUANTITY_MIN = 2_000;
    private const long _LAYER_QUANTITY_MAX = 8_000;

    #region Common rolls and single quantity

    [Test, Description("A placement the editor set a quantity on keeps that quantity, so an authored value is never rolled over.")]
    public void ShouldUseEditorQuantityWhenPlacementCarriesOne()
    {
        const long authored = 6_000_000;

        InstanceToCreate instance = PlanOne(BuildPlacement(initialQuantity: authored));

        Assert.That(instance.Quantity, Is.EqualTo(authored), "An authored quantity is the author's decision and must survive setup untouched.");
    }

    [Test, Description("A placement with no authored quantity is rolled within its habitat's initial range, which is what makes one map vary between games.")]
    public void ShouldRollQuantityWithinHabitatRangeWhenPlacementCarriesNone()
    {
        InstanceToCreate instance = PlanOne(BuildPlacement());

        Assert.That(instance.Quantity, Is.InRange(_QUANTITY_MIN, _QUANTITY_MAX), "An unauthored quantity comes from the habitat's range.");
    }

    [Test, Description("A habitat whose initial range is a single value yields exactly that value, since a roll must include its upper bound.")]
    public void ShouldRollExactValueWhenHabitatRangeIsOneValue()
    {
        const long only = 5_000;

        MapResourceTypeValues type = BuildType(habitat: new HabitatValues(only, only, _SPREAD_LIMIT_MIN, _SPREAD_LIMIT_MAX));

        InstanceToCreate instance = PlanOne(BuildPlacement(), type);

        Assert.That(instance.Quantity, Is.EqualTo(only), "A range of one value can only produce that value; an exclusive upper bound would make it unreachable.");
    }

    [Test, Description("A habitat whose spread range is a single value yields exactly that value, for the same reason the quantity range does.")]
    public void ShouldRollExactSpreadLimitWhenHabitatRangeIsOneValue()
    {
        const int only = 3;

        MapResourceTypeValues type = BuildType(habitat: new HabitatValues(_QUANTITY_MIN, _QUANTITY_MAX, only, only));

        InstanceToCreate instance = PlanOne(BuildPlacement(), type);

        Assert.That(instance.SpreadLimit, Is.EqualTo(only), "A range of one value can only produce that value.");
    }

    [Test, Description("SpreadLimit is rolled within the habitat's range, and left null for a habitat carrying none, which is how a type that never spreads is said.")]
    public void ShouldRollSpreadLimitOnlyWhenHabitatCarriesRange()
    {
        InstanceToCreate spreads = PlanOne(BuildPlacement());

        MapResourceTypeValues neverSpreads = BuildType(habitat: new HabitatValues(_QUANTITY_MIN, _QUANTITY_MAX, null, null));
        InstanceToCreate doesNot = PlanOne(BuildPlacement(), neverSpreads);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(spreads.SpreadLimit, Is.InRange(_SPREAD_LIMIT_MIN, _SPREAD_LIMIT_MAX), "A habitat with a spread range rolls within it.");
            Assert.That(doesNot.SpreadLimit, Is.Null, "A habitat with no spread range means the type never spreads.");
        }
    }

    [Test, Description("EstimationFuzzyFactor is rolled within the type's range, which is what makes a Player's view of a quantity an estimate rather than the figure.")]
    public void ShouldRollEstimationFuzzyFactorWithinTypeRange()
    {
        InstanceToCreate instance = PlanOne(BuildPlacement());

        Assert.That(instance.EstimationFuzzyFactor, Is.InRange(_FUZZY_MIN, _FUZZY_MAX), "The factor comes from the type's own range.");
    }

    [Test, Description("A type carrying no fuzzy range leaves the factor null, rather than standing an unrolled zero in for it.")]
    public void ShouldLeaveEstimationFuzzyFactorNullWhenTypeCarriesNoRange()
    {
        MapResourceTypeValues type = BuildType(fuzzyMin: null, fuzzyMax: null);

        InstanceToCreate instance = PlanOne(BuildPlacement(), type);

        Assert.That(instance.EstimationFuzzyFactor, Is.Null, "A zero factor would claim the quantity is shown exactly, which is a different statement from having no range.");
    }

    [Test, Description("The tile id is the map's offset plus the tile's row-major position, matching the ids the campaign-map copy assigned.")]
    public void ShouldAddressTileByOffsetAndRowMajorPosition()
    {
        InstanceToCreate instance = PlanOne(BuildPlacement());

        Assert.That(instance.CampaignMapId, Is.EqualTo(_EXPECTED_TILE_ID), "An instance on the wrong tile is worse than none at all.");
    }

    [Test, Description("Two placements on one tile roll different values, because the slot is part of every roll's key.")]
    public void ShouldRollDifferentValuesForEachSlotOnOneTile()
    {
        MapResourceInstantiationPlan plan = Plan([BuildPlacement(slot: 1), BuildPlacement(slot: 2, placementId: 2)]);

        List<InstanceToCreate> instances = [.. plan.ToCreate];

        using (Assert.EnterMultipleScope())
        {
            Assert.That(instances, Has.Count.EqualTo(2), "Both slots should produce an instance.");
            Assert.That(
                instances[0].Quantity,
                Is.Not.EqualTo(instances[1].Quantity),
                "Omitting the slot from the roll's key would give both slots on a tile the identical quantity.");
        }
    }

    [Test, Description("One seed and one tile roll identical values on every run, which is what makes a Game reproducible from its seed.")]
    public void ShouldRollIdenticalValuesWhenSeedAndTileRepeat()
    {
        InstanceToCreate first = PlanOne(BuildPlacement());
        InstanceToCreate second = PlanOne(BuildPlacement());

        using (Assert.EnterMultipleScope())
        {
            Assert.That(second.Quantity, Is.EqualTo(first.Quantity), "The same seed and tile must roll the same quantity.");
            Assert.That(second.SpreadLimit, Is.EqualTo(first.SpreadLimit), "The same seed and tile must roll the same spread limit.");
            Assert.That(second.EstimationFuzzyFactor, Is.EqualTo(first.EstimationFuzzyFactor), "The same seed and tile must roll the same fuzzy factor.");
        }
    }

    [Test, Description("A different seed rolls different values on the same tile, so one map does not play out the same way twice.")]
    public void ShouldRollDifferentValuesWhenSeedDiffers()
    {
        InstanceToCreate first = PlanOne(BuildPlacement());
        InstanceToCreate second = PlanOne(BuildPlacement(), seed: _SEED + 1);

        Assert.That(second.Quantity, Is.Not.EqualTo(first.Quantity), "Two Games from one map must differ, or the seed decides nothing.");
    }

    [Test, Description("A placement whose tile no habitat covers is refused rather than created, which is what repainting a Biome leaves behind.")]
    public void ShouldRefusePlacementWhenNoHabitatCoversTile()
    {
        MapResourceInstantiationPlan plan = Plan([BuildPlacement(biome: BiomeType.Desert)]);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(plan.ToCreate, Is.Empty, "A type with no habitat for the tile cannot exist there.");
            Assert.That(plan.Refusals.Single().Reason, Is.EqualTo(MapResourceInstantiationRefusal.NoHabitat), "The refusal names what an author has to fix.");
            Assert.That(plan.Refusals.Single().PlacementId, Is.EqualTo(1), "The refusal carries the placement, so a log can point at it.");
        }
    }

    [Test, Description("A placement naming a MapResourceType the catalogue does not hold is refused, not thrown on, so one stale row cannot fail a Game's setup.")]
    public void ShouldRefusePlacementWhenTypeIsNotInCatalogue()
    {
        MapResourceInstantiationPlan plan = MapResourceInstantiationRules.Plan(
            _SEED,
            _OFFSET,
            _MAP_WIDTH,
            [BuildPlacement()],
            new Dictionary<int, MapResourceTypeValues>());

        using (Assert.EnterMultipleScope())
        {
            Assert.That(plan.ToCreate, Is.Empty, "Nothing is known about the type, so nothing can be created.");
            Assert.That(plan.Refusals.Single().Reason, Is.EqualTo(MapResourceInstantiationRefusal.NoHabitat), "A type that is absent has no habitat covering the tile.");
        }
    }

    [Test, Description("A single-quantity instance holds its quantity itself and carries no Distribution or layer rows.")]
    public void ShouldCreateNoChildRowsForSingleQuantityType()
    {
        InstanceToCreate instance = PlanOne(BuildPlacement());

        using (Assert.EnterMultipleScope())
        {
            Assert.That(instance.Amounts, Is.Empty, "Only a Distribution holds its amounts in child rows.");
            Assert.That(instance.Layers, Is.Empty, "Only a stratified type holds its quantity in layers.");
            Assert.That(instance.MapResourceDistributionScenarioId, Is.Null, "Only a Distribution is created from a scenario.");
        }
    }

    #endregion

    #region Distribution

    [Test, Description("A Distribution the editor pinned a scenario on keeps it, so an authored mix is never rolled over.")]
    public void ShouldUseEditorScenarioWhenPlacementPinsOne()
    {
        InstanceToCreate instance = PlanOne(BuildWoodPlacement(scenarioId: _RARE_SCENARIO_ID), BuildDistributionType());

        using (Assert.EnterMultipleScope())
        {
            Assert.That(instance.MapResourceDistributionScenarioId, Is.EqualTo(_RARE_SCENARIO_ID), "An authored scenario is the author's decision.");
            Assert.That(instance.Amounts.Select(a => a.AssetId), Is.EquivalentTo(new[] { _RARE_ASSET_ID }), "The amounts must come from the pinned scenario.");
        }
    }

    [Test, Description("A pinned scenario belonging to another MapResourceType is refused, since the foreign key proves only that the scenario exists.")]
    public void ShouldRefusePlacementWhenPinnedScenarioBelongsToAnotherType()
    {
        const int scenarioOfSomeOtherType = 9999;

        MapResourceInstantiationPlan plan = Plan([BuildWoodPlacement(scenarioId: scenarioOfSomeOtherType)], BuildDistributionType());

        using (Assert.EnterMultipleScope())
        {
            Assert.That(plan.ToCreate, Is.Empty, "A scenario from another type says nothing about what this tile holds.");
            Assert.That(plan.Refusals.Single().Reason, Is.EqualTo(MapResourceInstantiationRefusal.ScenarioNotOfType), "The refusal names what an author has to fix.");
        }
    }

    [Test, Description("An unpinned Distribution picks among the scenarios weighted for its tile's Biome, and never one weighted only for another.")]
    public void ShouldPickScenarioWeightedForTileBiomeWhenPlacementPinsNone()
    {
        // The jungle scenario carries no Forest weight, so a Forest tile must never draw it.
        MapResourceTypeValues type = BuildDistributionType(withJungleOnlyScenario: true);

        MapResourceInstantiationPlan plan = Plan(BuildPlacementsAcrossTiles(_WEIGHT_SAMPLE_TILES), type);

        int[] drawn = [.. plan.ToCreate.Select(instance => instance.MapResourceDistributionScenarioId ?? 0)];

        using (Assert.EnterMultipleScope())
        {
            Assert.That(drawn, Has.Length.EqualTo(_WEIGHT_SAMPLE_TILES), "Every Forest tile should hold a Distribution instance.");
            Assert.That(drawn, Has.None.Zero, "An instance with no scenario means the pick never happened.");
            Assert.That(
                drawn,
                Has.None.EqualTo(_JUNGLE_SCENARIO_ID),
                "A scenario with no weight for the tile's Biome is what keeps bamboo out of forests.");
        }
    }

    [Test, Description("The weights decide how often each scenario is picked, so a rare mix stays rare across many tiles.")]
    public void ShouldPickScenariosInProportionToTheirWeights()
    {
        MapResourceInstantiationPlan plan = Plan(BuildPlacementsAcrossTiles(_WEIGHT_SAMPLE_TILES), BuildDistributionType());

        int commonCount = plan.ToCreate.Count(instance => instance.MapResourceDistributionScenarioId == _COMMON_SCENARIO_ID);
        int rareCount = plan.ToCreate.Count(instance => instance.MapResourceDistributionScenarioId == _RARE_SCENARIO_ID);

        using (Assert.EnterMultipleScope())
        {
            // Weighted 20 to 1, so over this many tiles the common mix should dominate by far more than the threefold asserted here.
            Assert.That(commonCount, Is.GreaterThan(rareCount * 3), "A weight of 20 against 1 must show as the common scenario dominating.");
            Assert.That(rareCount, Is.GreaterThan(0), "A weight of 1 is rare, not impossible; never drawing it would mean the weights are being read as a filter.");
        }
    }

    [Test, Description("A Distribution whose Biome carries no scenario weight is refused, because the weighted pick has nothing to choose from.")]
    public void ShouldRefusePlacementWhenBiomeHasNoScenarioWeight()
    {
        // Woodland is a habitat of the type, so the tile is compatible; no scenario is weighted for it.
        MapResourceInstantiationPlan plan = Plan([BuildWoodPlacement(biome: BiomeType.Woodland)], BuildDistributionType());

        using (Assert.EnterMultipleScope())
        {
            Assert.That(plan.ToCreate, Is.Empty, "There is no mix to create the instance from.");
            Assert.That(plan.Refusals.Single().Reason, Is.EqualTo(MapResourceInstantiationRefusal.NoScenarioForBiome), "Distinct from NoHabitat: the tile is compatible, the catalogue is short.");
        }
    }

    [Test, Description("A Distribution whose candidate weights all total zero is refused, since a weighted pick cannot divide by zero.")]
    public void ShouldRefusePlacementWhenCandidateWeightsSumToZero()
    {
        MapResourceTypeValues type = BuildDistributionType(commonWeight: 0, rareWeight: 0);

        MapResourceInstantiationPlan plan = Plan([BuildWoodPlacement()], type);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(plan.ToCreate, Is.Empty, "A total of zero leaves nothing to pick.");
            Assert.That(plan.Refusals.Single().Reason, Is.EqualTo(MapResourceInstantiationRefusal.NoScenarioForBiome), "Weighted at zero everywhere is the same as not being weighted for the Biome at all.");
        }
    }

    [Test, Description("A Distribution copies its scenario's amounts onto the instance and holds no single quantity of its own.")]
    public void ShouldCopyScenarioAmountsAndLeaveQuantityNull()
    {
        InstanceToCreate instance = PlanOne(BuildWoodPlacement(scenarioId: _COMMON_SCENARIO_ID), BuildDistributionType());

        using (Assert.EnterMultipleScope())
        {
            Assert.That(instance.Quantity, Is.Null, "A Distribution's quantity lives in its amount rows.");
            Assert.That(instance.Amounts, Has.Count.EqualTo(2), "The common scenario holds two Assets.");
            Assert.That(instance.Amounts.Single(a => a.AssetId == _COMMON_ASSET_ID).Amount, Is.EqualTo(_COMMON_ASSET_AMOUNT), "The amounts are copied from the scenario exactly.");
            Assert.That(instance.Layers, Is.Empty, "A Distribution holds no layers.");
        }
    }

    [Test, Description("A Distribution never spreads, so it is created with no SpreadLimit whatever its habitat carries.")]
    public void ShouldLeaveSpreadLimitNullForDistribution()
    {
        InstanceToCreate instance = PlanOne(BuildWoodPlacement(scenarioId: _COMMON_SCENARIO_ID), BuildDistributionType());

        Assert.That(instance.SpreadLimit, Is.Null, "The habitat carries a spread range, and a Distribution must still ignore it.");
    }

    [Test, Description("An editor-set quantity on a Distribution is ignored, because its amounts come from a scenario rather than one figure.")]
    public void ShouldIgnoreEditorQuantityWhenTypeIsDistribution()
    {
        InstanceToCreate instance = PlanOne(BuildWoodPlacement(initialQuantity: 6_000_000, scenarioId: _COMMON_SCENARIO_ID), BuildDistributionType());

        Assert.That(instance.Quantity, Is.Null, "A single quantity has no meaning for a type that holds several Assets.");
    }

    [Test, Description("The Distribution pick follows the scenarios' ScenarioNumber rather than the order they were read in, so one seed agrees across databases.")]
    public void ShouldPickSameScenarioWhateverOrderScenariosWereReadIn()
    {
        // Equal weights, so reversing the list moves roughly half the picks if the cumulative walk follows read order.
        MapResourceTypeValues asRead = BuildDistributionType(commonWeight: 1, rareWeight: 1);
        MapResourceTypeValues reversed = BuildDistributionType(commonWeight: 1, rareWeight: 1, reverseScenarioOrder: true);

        MapResourceInstantiationPlan fromAsRead = Plan(BuildPlacementsAcrossTiles(_WEIGHT_SAMPLE_TILES), asRead);
        MapResourceInstantiationPlan fromReversed = Plan(BuildPlacementsAcrossTiles(_WEIGHT_SAMPLE_TILES), reversed);

        Assert.That(
            fromReversed.ToCreate.Select(instance => instance.MapResourceDistributionScenarioId),
            Is.EqualTo(fromAsRead.ToCreate.Select(instance => instance.MapResourceDistributionScenarioId)),
            "Scenario identity order is a database accident, so the pick must not depend on it.");
    }

    [Test, Description("A spread range of int.MaxValue rolls that value rather than throwing, so an extreme catalogue cannot fail a Game's setup.")]
    public void ShouldRollSpreadLimitWhenHabitatRangeIsIntMaxValue()
    {
        MapResourceTypeValues type = BuildType(habitat: new HabitatValues(_QUANTITY_MIN, _QUANTITY_MAX, int.MaxValue, int.MaxValue));

        InstanceToCreate instance = PlanOne(BuildPlacement(), type);

        Assert.That(instance.SpreadLimit, Is.EqualTo(int.MaxValue), "Making the bound inclusive must not overflow the int it is rolled as.");
    }

    [Test, Description("A single-quantity placement with no authored quantity and no habitat range is refused, since the instance would hold nothing and block a slot.")]
    public void ShouldRefusePlacementWhenSingleQuantityTypeHasNoQuantityRange()
    {
        MapResourceTypeValues noRange = BuildType(habitat: new HabitatValues(null, null, _SPREAD_LIMIT_MIN, _SPREAD_LIMIT_MAX));

        MapResourceInstantiationPlan plan = Plan([BuildPlacement()], noRange);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(plan.ToCreate, Is.Empty, "An instance with a null Quantity would be unextractable and would hold its slot for the Game's life.");
            Assert.That(plan.Refusals.Single().Reason, Is.EqualTo(MapResourceInstantiationRefusal.NoQuantityRange), "The refusal names the catalogue gap.");
        }
    }

    [Test, Description("A placement the editor set a quantity on is created even where its habitat names no range, because the authored value is what it holds.")]
    public void ShouldCreateInstanceWhenHabitatHasNoRangeButEditorSetQuantity()
    {
        const long authored = 1_234_000;

        MapResourceTypeValues noRange = BuildType(habitat: new HabitatValues(null, null, _SPREAD_LIMIT_MIN, _SPREAD_LIMIT_MAX));

        InstanceToCreate instance = PlanOne(BuildPlacement(initialQuantity: authored), noRange);

        Assert.That(instance.Quantity, Is.EqualTo(authored), "There is nothing to roll, but nothing needs rolling.");
    }

    [Test, Description("A refusal carries the MapResourceType it concerns, so the log can name what an author has to fix.")]
    public void ShouldCarryTypeOnRefusal()
    {
        MapResourceInstantiationPlan plan = Plan([BuildPlacement(biome: BiomeType.Desert)]);

        RefusedPlacement refusal = plan.Refusals.Single();

        using (Assert.EnterMultipleScope())
        {
            Assert.That(refusal.MapResourceTypeId, Is.EqualTo(_BERRY_TYPE_ID), "A reason and a coordinate alone leave an author guessing which type it was.");
            Assert.That(refusal.MapResourceTypeDisplayName, Is.EqualTo("Test Berry"), "The name is what a log reader recognises.");
        }
    }

    #endregion

    #region Stratified

    [Test, Description("A stratified instance gets one layer per layer its type defines, holding its quantity in them rather than in one figure.")]
    public void ShouldCreateOneLayerPerTypeLayerAndLeaveQuantityNull()
    {
        InstanceToCreate instance = PlanOne(BuildCoalPlacement(), BuildStratifiedType());

        using (Assert.EnterMultipleScope())
        {
            Assert.That(instance.Layers, Has.Count.EqualTo(_COAL_LAYER_COUNT), "One row per layer template.");
            Assert.That(instance.Layers.Select(layer => layer.LayerNumber), Is.EquivalentTo(new[] { 1, 2, 3 }), "The layers keep their depths, which is the order they are worked in.");
            Assert.That(instance.Quantity, Is.Null, "A stratified instance's quantity lives in its layers.");
            Assert.That(instance.Amounts, Is.Empty, "Only a Distribution holds amount rows.");
        }
    }

    [Test, Description("A layer that rolls empty keeps its row with no Asset and nothing in it, so a survey can still reveal that it is empty.")]
    public void ShouldCreateEmptyLayerWithNullAssetAndZeroQuantityWhenEmptyRollSucceeds()
    {
        MapResourceTypeValues alwaysEmpty = BuildStratifiedType(emptyLikelihood: 1.0m);

        InstanceToCreate instance = PlanOne(BuildCoalPlacement(), alwaysEmpty);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(instance.Layers, Has.Count.EqualTo(_COAL_LAYER_COUNT), "An empty layer is still a layer.");
            Assert.That(instance.Layers.Select(layer => layer.AssetId), Has.All.Null, "An empty layer holds no Asset.");
            Assert.That(instance.Layers.Select(layer => layer.Quantity), Has.All.Zero, "CK_MapResourceInstanceLayer_Quantity requires an Asset-less layer to hold zero.");
        }
    }

    [Test, Description("A layer that is not empty is rolled within its own quantity range, inclusive of both bounds.")]
    public void ShouldRollLayerQuantityWithinItsRangeWhenNotEmpty()
    {
        MapResourceTypeValues neverEmpty = BuildStratifiedType(emptyLikelihood: 0m);

        InstanceToCreate instance = PlanOne(BuildCoalPlacement(), neverEmpty);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(instance.Layers, Has.Count.EqualTo(_COAL_LAYER_COUNT), "Every layer template should produce a layer.");
            Assert.That(instance.Layers.Select(layer => layer.AssetId), Has.None.Null, "A layer that is not empty holds its template's Asset.");
            Assert.That(
                instance.Layers.Select(layer => layer.Quantity),
                Has.All.InRange(_LAYER_QUANTITY_MIN, _LAYER_QUANTITY_MAX),
                "Each layer's quantity comes from its own range.");
        }
    }

    [Test, Description("Each layer carries its own fuzzy factor, so a deeper layer can be harder to judge than the one above it.")]
    public void ShouldRollFuzzyFactorPerLayer()
    {
        MapResourceTypeValues neverEmpty = BuildStratifiedType(emptyLikelihood: 0m);

        InstanceToCreate instance = PlanOne(BuildCoalPlacement(), neverEmpty);

        decimal[] factors = [.. instance.Layers.Select(layer => layer.EstimationFuzzyFactor)];

        using (Assert.EnterMultipleScope())
        {
            Assert.That(factors, Has.All.InRange(_FUZZY_MIN, _FUZZY_MAX), "Each factor comes from its layer's range.");
            Assert.That(
                factors.Distinct().Count(),
                Is.GreaterThan(1),
                "Layers rolling one identical factor would mean the layer number is missing from the roll's key.");
        }
    }

    [Test, Description("The instance itself carries no fuzzy factor when stratified, even where its type names a range, because its layers each have one.")]
    public void ShouldLeaveInstanceFuzzyFactorNullWhenStratified()
    {
        // The type names a range, so a null factor can only come from the instance being stratified rather than from there being nothing to roll.
        MapResourceTypeValues withTypeLevelRange = BuildStratifiedType(typeFuzzyMin: _FUZZY_MIN, typeFuzzyMax: _FUZZY_MAX);

        InstanceToCreate instance = PlanOne(BuildCoalPlacement(), withTypeLevelRange);

        Assert.That(instance.EstimationFuzzyFactor, Is.Null, "One factor for the instance would contradict the per-layer ones.");
    }

    [Test, Description("A stratified type with no layers is refused, since the instance would hold nothing at all.")]
    public void ShouldRefusePlacementWhenStratifiedTypeHasNoLayers()
    {
        MapResourceTypeValues noLayers = BuildStratifiedType(layerCount: 0);

        MapResourceInstantiationPlan plan = Plan([BuildCoalPlacement()], noLayers);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(plan.ToCreate, Is.Empty, "An instance with no layers and no quantity holds nothing.");
            Assert.That(plan.Refusals.Single().Reason, Is.EqualTo(MapResourceInstantiationRefusal.NoLayers), "The refusal names the catalogue gap.");
        }
    }

    [Test, Description("An editor-set quantity on a stratified placement is ignored, because its layers are always rolled.")]
    public void ShouldIgnoreEditorQuantityWhenTypeIsStratified()
    {
        InstanceToCreate instance = PlanOne(BuildCoalPlacement(initialQuantity: 6_000_000), BuildStratifiedType());

        Assert.That(instance.Quantity, Is.Null, "A single quantity has no meaning for a type held in layers.");
    }

    [Test, Description("A stratified instance never spreads, so it is created with no SpreadLimit whatever its habitat carries.")]
    public void ShouldLeaveSpreadLimitNullWhenStratified()
    {
        InstanceToCreate instance = PlanOne(BuildCoalPlacement(), BuildStratifiedType());

        Assert.That(instance.SpreadLimit, Is.Null, "Stratified and renewable are mutually exclusive, so nothing stratified spreads.");
    }

    #endregion

    #region Helpers

    private static MapResourceInstantiationPlan Plan(
        IReadOnlyCollection<PlacementToInstantiate> placements,
        MapResourceTypeValues? type = null,
        long seed = _SEED)
    {
        MapResourceTypeValues resolved = type ?? BuildType();

        return MapResourceInstantiationRules.Plan(
            seed,
            _OFFSET,
            _MAP_WIDTH,
            placements,
            new Dictionary<int, MapResourceTypeValues> { [resolved.MapResourceTypeId] = resolved });
    }

    /// <summary>
    /// The one instance a single-placement plan creates, failing the test rather than throwing when it refused instead.
    /// </summary>
    private static InstanceToCreate PlanOne(PlacementToInstantiate placement, MapResourceTypeValues? type = null, long seed = _SEED)
    {
        MapResourceInstantiationPlan plan = Plan([placement], type, seed);

        Assert.That(plan.ToCreate, Has.Count.EqualTo(1), () => $"Expected one instance; refusals: {string.Join("; ", plan.Refusals.Select(r => r.Reason))}.");

        return plan.ToCreate.Single();
    }

    private static PlacementToInstantiate BuildPlacement(
        long? initialQuantity = null,
        byte slot = 1,
        int placementId = 1,
        BiomeType biome = BiomeType.Forest,
        CampaignMapGeographyType geography = CampaignMapGeographyType.Flat,
        int? scenarioId = null,
        int mapResourceTypeId = _BERRY_TYPE_ID,
        int x = _TILE_X,
        int y = _TILE_Y)
    {
        return new PlacementToInstantiate(
            placementId,
            x,
            y,
            slot,
            mapResourceTypeId,
            initialQuantity,
            scenarioId,
            biome,
            geography);
    }

    /// <summary>
    /// A placement of the Distribution type, which the Distribution tests plan against.
    /// </summary>
    private static PlacementToInstantiate BuildWoodPlacement(
        int? scenarioId = null,
        long? initialQuantity = null,
        BiomeType biome = BiomeType.Forest)
    {
        return BuildPlacement(
            initialQuantity: initialQuantity,
            biome: biome,
            scenarioId: scenarioId,
            mapResourceTypeId: _WOOD_TYPE_ID);
    }

    /// <summary>
    /// A Distribution holding a common mix and a rare one, both weighted for Forest, plus a habitat for Woodland that no scenario covers.
    /// </summary>
    /// <remarks>
    /// The Woodland habitat is deliberate: it makes a Woodland tile compatible with the type while leaving the weighted pick nothing to draw, which is the
    /// only way to tell <c>NoScenarioForBiome</c> apart from <c>NoHabitat</c>.
    /// </remarks>
    private static MapResourceTypeValues BuildDistributionType(
        int commonWeight = 20,
        int rareWeight = 1,
        bool withJungleOnlyScenario = false,
        bool reverseScenarioOrder = false)
    {
        List<ScenarioValues> scenarios =
        [
            new ScenarioValues(
                _COMMON_SCENARIO_ID,
                ScenarioNumber: 1,
                new Dictionary<BiomeType, int> { [BiomeType.Forest] = commonWeight },
                [new AmountToCreate(_COMMON_ASSET_ID, _COMMON_ASSET_AMOUNT), new AmountToCreate(_GENERIC_ASSET_ID, 10_000_000)]),
            new ScenarioValues(
                _RARE_SCENARIO_ID,
                ScenarioNumber: 2,
                new Dictionary<BiomeType, int> { [BiomeType.Forest] = rareWeight },
                [new AmountToCreate(_RARE_ASSET_ID, 300_000)])
        ];

        if (withJungleOnlyScenario)
        {
            scenarios.Add(new ScenarioValues(
                _JUNGLE_SCENARIO_ID,
                ScenarioNumber: 3,
                new Dictionary<BiomeType, int> { [BiomeType.Jungle] = 25 },
                [new AmountToCreate(_GENERIC_ASSET_ID, 1_000)]));
        }

        if (reverseScenarioOrder)
        {
            scenarios.Reverse();
        }

        return new MapResourceTypeValues(
            _WOOD_TYPE_ID,
            "Test Wood",
            IsStratified: false,
            IsDistribution: true,
            _FUZZY_MIN,
            _FUZZY_MAX,
            new Dictionary<HabitatScope, HabitatValues>
            {
                [new HabitatScope(BiomeType.Forest, null)] = new HabitatValues(_QUANTITY_MIN, _QUANTITY_MAX, _SPREAD_LIMIT_MIN, _SPREAD_LIMIT_MAX),
                [new HabitatScope(BiomeType.Woodland, null)] = new HabitatValues(_QUANTITY_MIN, _QUANTITY_MAX, _SPREAD_LIMIT_MIN, _SPREAD_LIMIT_MAX)
            },
            scenarios,
            Layers: []);
    }

    /// <summary>
    /// A placement of the stratified type, which the stratified tests plan against.
    /// </summary>
    private static PlacementToInstantiate BuildCoalPlacement(long? initialQuantity = null)
    {
        return BuildPlacement(initialQuantity: initialQuantity, mapResourceTypeId: _COAL_TYPE_ID);
    }

    /// <summary>
    /// A stratified type whose layers each hold their own Asset and share one set of ranges.
    /// </summary>
    /// <param name="emptyLikelihood">Driven to 0 or 1 by the tests that need the empty roll settled rather than sampled.</param>
    private static MapResourceTypeValues BuildStratifiedType(
        decimal emptyLikelihood = 0.5m,
        int layerCount = _COAL_LAYER_COUNT,
        decimal? typeFuzzyMin = null,
        decimal? typeFuzzyMax = null)
    {
        List<LayerValues> layers =
        [
            .. Enumerable.Range(1, layerCount).Select(layerNumber => new LayerValues(
                layerNumber,
                AssetId: 50 + layerNumber,
                emptyLikelihood,
                _LAYER_QUANTITY_MIN,
                _LAYER_QUANTITY_MAX,
                _FUZZY_MIN,
                _FUZZY_MAX))
        ];

        return new MapResourceTypeValues(
            _COAL_TYPE_ID,
            "Test Coal",
            IsStratified: true,
            IsDistribution: false,
            // Normally unset on a stratified type, since each of its layers carries its own; one test sets it to prove it is still ignored.
            typeFuzzyMin,
            typeFuzzyMax,
            new Dictionary<HabitatScope, HabitatValues>
            {
                [new HabitatScope(BiomeType.Forest, null)] = new HabitatValues(_QUANTITY_MIN, _QUANTITY_MAX, _SPREAD_LIMIT_MIN, _SPREAD_LIMIT_MAX)
            },
            Scenarios: [],
            layers);
    }

    /// <summary>
    /// One Wood placement on each of <paramref name="tileCount"/> distinct tiles, for the tests that need a sample of rolls rather than one.
    /// </summary>
    private static List<PlacementToInstantiate> BuildPlacementsAcrossTiles(int tileCount, BiomeType biome = BiomeType.Forest)
    {
        return [.. Enumerable.Range(0, tileCount).Select(index => BuildPlacement(
            placementId: index + 1,
            biome: biome,
            mapResourceTypeId: _WOOD_TYPE_ID,
            x: index % _MAP_WIDTH,
            y: index / _MAP_WIDTH))];
    }

    private static MapResourceTypeValues BuildType(
        HabitatValues? habitat = null,
        decimal? fuzzyMin = _FUZZY_MIN,
        decimal? fuzzyMax = _FUZZY_MAX)
    {
        return new MapResourceTypeValues(
            _BERRY_TYPE_ID,
            "Test Berry",
            IsStratified: false,
            IsDistribution: false,
            fuzzyMin,
            fuzzyMax,
            new Dictionary<HabitatScope, HabitatValues>
            {
                [new HabitatScope(BiomeType.Forest, null)] = habitat ?? new HabitatValues(_QUANTITY_MIN, _QUANTITY_MAX, _SPREAD_LIMIT_MIN, _SPREAD_LIMIT_MAX)
            },
            Scenarios: [],
            Layers: []);
    }

    #endregion
}
