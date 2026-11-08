using AscendantEmpire.Application.Features.Maps.MapResources.Classes;
using AscendantEmpire.Application.Helpers;
using AscendantEmpire.Application.Helpers.Randomness;

namespace AscendantEmpire.Application.Features.Maps.MapResources.Services;

/// <summary>
/// Turns a map's MapResource placements into the instances one Game starts with, rolling whatever the map editor left unset.
/// </summary>
/// <remarks>
/// <para>
/// Pure and in-memory, for the reason <see cref="BiomeImpliedResourceRules"/> is: the rolls are the whole of this feature, and every one of them must be
/// pinnable without a database behind it. See <c>docs/design/campaign-map/map-resources.md</c>.
/// </para>
/// <para>
/// Every roll keys on the tile's coordinates and slot rather than the instance's <c>Id</c>, which is assigned by the database and so does not exist yet.
/// Coordinates are also the only key that is stable between databases: a tile's <c>CampaignMapId</c> depends on how many maps the database already holds, so
/// keying on it would let one seed and one map produce different Games in different databases.
/// </para>
/// </remarks>
internal static class MapResourceInstantiationRules
{
    /// <summary>
    /// The decimal places the fuzzy-factor columns hold, which a rolled fraction is rounded to so the stored value is the rolled one.
    /// </summary>
    private const int _FRACTION_DECIMAL_PLACES = 3;

    /// <summary>
    /// Stands in for the display name of a type the catalogue does not hold, since a refusal for an absent type has no name to carry.
    /// </summary>
    private const string _ABSENT_TYPE_NAME = "(type not in catalogue)";

    /// <summary>
    /// Every instance the Game should be created with, from its map's placements, and every placement that could not become one.
    /// </summary>
    /// <param name="gameSeed">The Game's <c>GenerationSeed</c>, which every roll derives from.</param>
    /// <param name="campaignMapOffsetId">The Game's world-map id for the tile at (0, 0).</param>
    /// <param name="typesById">The MapResourceTypes the placements name. A placement naming one that is absent is refused rather than thrown on.</param>
    internal static MapResourceInstantiationPlan Plan(
        long gameSeed,
        int campaignMapOffsetId,
        int mapWidth,
        IReadOnlyCollection<PlacementToInstantiate> placements,
        IReadOnlyDictionary<int, MapResourceTypeValues> typesById)
    {
        List<InstanceToCreate> toCreate = [];
        List<RefusedPlacement> refusals = [];

        foreach (PlacementToInstantiate placement in placements)
        {
            // A type the catalogue does not hold has no habitat covering the tile, which is the same refusal for the same reason.
            if (typesById.TryGetValue(placement.MapResourceTypeId, out MapResourceTypeValues? type) == false)
            {
                refusals.Add(Refuse(placement, type: null, MapResourceInstantiationRefusal.NoHabitat));
                continue;
            }

            HabitatScope? scope = BiomeImpliedResourceRules.FindHabitat(
                type.Habitats.Keys,
                placement.BiomeTypeId,
                placement.CampaignMapGeographyTypeId);

            if (scope == null)
            {
                refusals.Add(Refuse(placement, type, MapResourceInstantiationRefusal.NoHabitat));
                continue;
            }

            HabitatValues habitat = type.Habitats[scope.Value];
            int campaignMapId = MapMethods.GetDbId(campaignMapOffsetId, mapWidth, placement.XCoord, placement.YCoord);

            if (type.IsStratified)
            {
                if (type.Layers.Count == 0)
                {
                    refusals.Add(Refuse(placement, type, MapResourceInstantiationRefusal.NoLayers));
                    continue;
                }

                // No quantity, no spread limit and no factor of its own: a stratified instance holds all three in its layers, and never spreads.
                toCreate.Add(new InstanceToCreate(
                    campaignMapId,
                    placement.Slot,
                    placement.MapResourceTypeId,
                    Quantity: null,
                    SpreadLimit: null,
                    EstimationFuzzyFactor: null,
                    MapResourceDistributionScenarioId: null,
                    Amounts: [],
                    RollLayers(gameSeed, placement, type)));

                continue;
            }

            if (type.IsDistribution)
            {
                (ScenarioValues? scenario, MapResourceInstantiationRefusal? scenarioRefusal) = ResolveScenario(gameSeed, placement, type);

                if (scenario == null)
                {
                    refusals.Add(Refuse(placement, type, scenarioRefusal!.Value));
                    continue;
                }

                // No quantity and no spread limit: a Distribution holds its amounts in child rows and never spreads, whatever its habitat carries.
                toCreate.Add(new InstanceToCreate(
                    campaignMapId,
                    placement.Slot,
                    placement.MapResourceTypeId,
                    Quantity: null,
                    SpreadLimit: null,
                    RollEstimationFuzzyFactor(gameSeed, placement, type),
                    scenario.ScenarioId,
                    scenario.Amounts,
                    Layers: []));

                continue;
            }

            long? quantity = RollQuantity(gameSeed, placement, habitat);

            if (quantity == null)
            {
                refusals.Add(Refuse(placement, type, MapResourceInstantiationRefusal.NoQuantityRange));
                continue;
            }

            toCreate.Add(new InstanceToCreate(
                campaignMapId,
                placement.Slot,
                placement.MapResourceTypeId,
                quantity,
                RollSpreadLimit(gameSeed, placement, habitat),
                RollEstimationFuzzyFactor(gameSeed, placement, type),
                MapResourceDistributionScenarioId: null,
                Amounts: [],
                Layers: []));
        }

        return new MapResourceInstantiationPlan(toCreate, refusals);
    }

    /// <summary>
    /// Each layer a stratified instance is created holding, with its contents rolled from its own template.
    /// </summary>
    /// <remarks>
    /// The layer number joins the roll's key, so two layers of one instance never roll alike. A layer that rolls empty keeps its row, with no Asset and
    /// nothing in it, because a survey can reveal that a layer is empty.
    /// </remarks>
    private static List<LayerToCreate> RollLayers(long gameSeed, PlacementToInstantiate placement, MapResourceTypeValues type)
    {
        List<LayerToCreate> layers = [];

        foreach (LayerValues template in type.Layers)
        {
            Random random = GameRandom.Create(
                gameSeed,
                RandomPurpose.MapResourceLayerContents,
                placement.XCoord,
                placement.YCoord,
                placement.Slot,
                template.LayerNumber);

            // Rolled first, and from this layer's own stream, so whether a layer is empty does not depend on how many layers sit above it.
            bool isEmpty = (decimal)random.NextDouble() < template.EmptyLikelihood;

            layers.Add(new LayerToCreate(
                template.LayerNumber,
                isEmpty ? null : template.AssetId,
                isEmpty ? 0 : random.NextInt64(template.QuantityMin, Exclusive(template.QuantityMax)),
                RollFraction(random, template.EstimationFuzzyFactorMin, template.EstimationFuzzyFactorMax)));
        }

        return layers;
    }

    /// <summary>
    /// The Distribution scenario the instance is created from: the map editor's, or one rolled among those weighted for the tile's Biome.
    /// </summary>
    /// <remarks>
    /// A pinned scenario is checked against the type's own scenarios, because the foreign key proves only that the scenario exists, not that it belongs to the
    /// type the placement names.
    /// </remarks>
    /// <returns>The scenario, or the reason no instance can be created.</returns>
    private static (ScenarioValues? Scenario, MapResourceInstantiationRefusal? Refusal) ResolveScenario(
        long gameSeed,
        PlacementToInstantiate placement,
        MapResourceTypeValues type)
    {
        if (placement.MapResourceDistributionScenarioId != null)
        {
            ScenarioValues? pinned = type.Scenarios.FirstOrDefault(scenario => scenario.ScenarioId == placement.MapResourceDistributionScenarioId);

            return pinned == null ? (null, MapResourceInstantiationRefusal.ScenarioNotOfType) : (pinned, null);
        }

        // Ordered by ScenarioNumber, not left as read: the cumulative walk below would otherwise pick by whatever order the database returned the rows in.
        List<ScenarioValues> candidates =
        [
            .. type.Scenarios
                .Where(scenario => scenario.BiomeWeights.ContainsKey(placement.BiomeTypeId))
                .OrderBy(scenario => scenario.ScenarioNumber)
        ];

        int totalWeight = candidates.Sum(scenario => scenario.BiomeWeights[placement.BiomeTypeId]);

        // A Biome with no weighted scenario and one whose scenarios are all weighted zero are the same thing: nothing to draw.
        if (totalWeight <= 0)
        {
            return (null, MapResourceInstantiationRefusal.NoScenarioForBiome);
        }

        Random random = Stream(gameSeed, RandomPurpose.MapResourceDistributionScenario, placement);
        int roll = random.Next(totalWeight);
        int running = 0;

        foreach (ScenarioValues candidate in candidates)
        {
            running += candidate.BiomeWeights[placement.BiomeTypeId];

            if (roll < running)
            {
                return (candidate, null);
            }
        }

        // Unreachable while the weights are non-negative: the roll is below the total, which the running sum reaches.
        return (candidates[^1], null);
    }

    /// <summary>
    /// The quantity the instance starts with: the map editor's, or a roll within the habitat's range.
    /// </summary>
    /// <returns>The quantity, or <see langword="null"/> where there is neither an authored value nor a range to roll from.</returns>
    private static long? RollQuantity(long gameSeed, PlacementToInstantiate placement, HabitatValues habitat)
    {
        if (placement.InitialQuantity != null)
        {
            return placement.InitialQuantity;
        }

        if (habitat.InitialQuantityMin == null || habitat.InitialQuantityMax == null)
        {
            return null;
        }

        Random random = Stream(gameSeed, RandomPurpose.MapResourceInitialQuantity, placement);

        return random.NextInt64(habitat.InitialQuantityMin.Value, Exclusive(habitat.InitialQuantityMax.Value));
    }

    /// <summary>
    /// How many spreads the instance starts able to make, or <see langword="null"/> for a habitat that carries no range.
    /// </summary>
    private static int? RollSpreadLimit(long gameSeed, PlacementToInstantiate placement, HabitatValues habitat)
    {
        if (habitat.SpreadLimitMin == null || habitat.SpreadLimitMax == null)
        {
            return null;
        }

        Random random = Stream(gameSeed, RandomPurpose.MapResourceSpreadLimit, placement);

        // Rolled as a long, because a habitat maximum of int.MaxValue has no int-sized exclusive bound to pass Next.
        return (int)random.NextInt64(habitat.SpreadLimitMin.Value, Exclusive(habitat.SpreadLimitMax.Value));
    }

    /// <summary>
    /// How far off the quantity shown to Players can be, or <see langword="null"/> for a type that carries no range.
    /// </summary>
    /// <remarks>
    /// Null rather than zero for a type with no range: zero would claim the quantity is shown exactly, which is a different statement.
    /// </remarks>
    private static decimal? RollEstimationFuzzyFactor(long gameSeed, PlacementToInstantiate placement, MapResourceTypeValues type)
    {
        if (type.EstimationFuzzyFactorMin == null || type.EstimationFuzzyFactorMax == null)
        {
            return null;
        }

        Random random = Stream(gameSeed, RandomPurpose.MapResourceEstimationFuzzyFactor, placement);

        return RollFraction(random, type.EstimationFuzzyFactorMin.Value, type.EstimationFuzzyFactorMax.Value);
    }

    /// <summary>
    /// A fraction within the range, rounded to what the column holds.
    /// </summary>
    /// <remarks>
    /// Rounded here rather than left to the database, so the value the rules decided is the value stored and a Player's estimate cannot shift by a rounding.
    /// </remarks>
    private static decimal RollFraction(Random random, decimal min, decimal max)
    {
        decimal rolled = min + ((max - min) * (decimal)random.NextDouble());

        return Math.Round(rolled, _FRACTION_DECIMAL_PLACES, MidpointRounding.AwayFromZero);
    }

    /// <summary>
    /// The exclusive upper bound that includes <paramref name="inclusiveMaximum"/> in the roll.
    /// </summary>
    /// <remarks>
    /// <see cref="Random.NextInt64(long, long)"/> excludes its upper bound, so passing a habitat's maximum straight in would make that maximum unreachable and
    /// a range of one value impossible to roll at all. A maximum of <see cref="long.MaxValue"/> has no successor to move to and so stays exclusive; no scaled
    /// quantity can reach it, and the alternative is throwing on a catalogue that is otherwise valid.
    /// </remarks>
    private static long Exclusive(long inclusiveMaximum)
    {
        return inclusiveMaximum == long.MaxValue ? long.MaxValue : inclusiveMaximum + 1;
    }

    private static Random Stream(long gameSeed, RandomPurpose purpose, PlacementToInstantiate placement)
    {
        return GameRandom.Create(gameSeed, purpose, placement.XCoord, placement.YCoord, placement.Slot);
    }

    /// <summary>
    /// A refusal carrying the type it concerns, so a log can name what an author has to fix.
    /// </summary>
    /// <param name="type">The placement's type, or <see langword="null"/> where the catalogue does not hold it, which is the one refusal with no type to name.</param>
    private static RefusedPlacement Refuse(PlacementToInstantiate placement, MapResourceTypeValues? type, MapResourceInstantiationRefusal reason)
    {
        return new RefusedPlacement(
            placement.PlacementId,
            placement.XCoord,
            placement.YCoord,
            placement.MapResourceTypeId,
            type?.DisplayName ?? _ABSENT_TYPE_NAME,
            reason);
    }
}
