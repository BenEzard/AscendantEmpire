using AscendantEmpire.Application.Common.Validation;
using AscendantEmpire.Application.Features.DatabaseSeeding.SeedData;
using AscendantEmpire.Application.Features.Maps.MapResources.Validators;
using AscendantEmpire.BackendContracts.Assets;
using AscendantEmpire.BackendContracts.MapResources.AddOrUpdateMapResourceType;
using AscendantEmpire.Domain.Enums.Maps;

namespace AscendantEmpire.Tests.Features.DatabaseSeeding;

/// <summary>
/// Tests that hold <b>every</b> seeded MapResource to the same invariants, whatever kind it is, so a resource added to
/// <see cref="MapResourceTypeSeedData"/> is checked without a line of new test code.
/// </summary>
/// <remarks>
/// <para>
/// No database — the seed data is a static builder and every check here is over the payload it returns before any handler sees it. Asset ids are stand-ins,
/// since nothing below the handler cares what they are.
/// </para>
/// <para>
/// These pin what the server-side validator cannot see: that two resources do not collide on a code, that the Assets they name are ones the Asset seed
/// really creates, that a Distribution's scenarios are numbered contiguously from 1, and that no scenario could be rolled onto a tile whose ceiling its
/// total exceeds. A resource's own shape is its own fixture's business — see <see cref="TimberSeedDataTests"/>.
/// </para>
/// </remarks>
[TestFixture]
internal class MapResourceTypeSeedDataTests
{
    /// <summary>
    /// The Asset standing for whatever a Biome itself offers to a forager, which no placed resource may yield.
    /// </summary>
    private const string _FORAGE = "Foodstuffs.PlantFoods.Forage";

    /// <summary>
    /// Stand-in Asset ids, one per code the seed names between all of its resources.
    /// </summary>
    private readonly Dictionary<string, int> _assetIdsByCode = MapResourceTypeSeedData.AssetCodes
        .Select((code, index) => (Code: code, Id: index + 1))
        .ToDictionary(asset => asset.Code, asset => asset.Id);

    /// <summary>
    /// Every seeded resource. Built in the constructor rather than a field initialiser, which cannot read another field.
    /// </summary>
    private readonly IReadOnlyCollection<AddOrUpdateMapResourceTypeDto> _types;

    public MapResourceTypeSeedDataTests()
    {
        _types = MapResourceTypeSeedData.Build(_assetIdsByCode).Types;
    }

    [Test, Description("Something is seeded, and each resource is counted once, so an empty or double-registered list is not quietly a successful seed.")]
    public void ShouldSeedEveryRegisteredResourceOnce()
    {
        Assert.Multiple(() =>
        {
            Assert.That(_types, Is.Not.Empty);
            Assert.That(_types, Has.Count.EqualTo(MapResourceTypeSeedData.ResourceCount));
            Assert.That(_types.Select(type => type.Code), Is.Unique, "Two resources share a code, and the upsert matches on code.");
        });
    }

    [Test, Description("The step's count is the rows the resources really contribute, since a count of 0 fails the step catalogue and a wrong one misleads the dialog.")]
    public void ShouldCountTheRowsTheResourcesActuallyProduce()
    {
        int rows = _types.Sum(type => type.DistributionScenarios.Count + type.Layers.Count + type.Yields.Count);

        Assert.Multiple(() =>
        {
            Assert.That(MapResourceTypeSeedData.ItemCount, Is.GreaterThan(0));
            Assert.That(MapResourceTypeSeedData.ItemCount, Is.EqualTo(rows));
        });
    }

    [Test, Description("Every resource passes the validator the catalogue handler runs, so a seed failure is never the first time a rule is heard about.")]
    public void ShouldProduceTypesTheValidatorAccepts()
    {
        var validator = new MapResourceTypeValidator();

        Assert.Multiple(() =>
        {
            foreach (AddOrUpdateMapResourceTypeDto type in _types)
            {
                ValidationResult result = validator.Validate(type);

                Assert.That(result.IsValid, Is.True, $"{type.Code}: {string.Join("; ", result.Errors.Select(error => error.ErrorMessage))}");
            }
        });
    }

    [Test, Description("Every resource can exist somewhere, with a habitat per Biome and Geography named once, since a type with none could never be placed.")]
    public void ShouldGiveEveryResourceAtLeastOneHabitat()
    {
        Assert.Multiple(() =>
        {
            foreach (AddOrUpdateMapResourceTypeDto type in _types)
            {
                Assert.That(type.Habitats, Is.Not.Empty, $"{type.Code} has no habitat, so it could never exist on a tile.");
                Assert.That(
                    type.Habitats.Select(habitat => (habitat.BiomeType, habitat.GeographyType)),
                    Is.Unique,
                    $"{type.Code} has two habitats for the same Biome and Geography.");
            }
        });
    }

    [Test, Description("A Distribution's scenarios are numbered from 1 with no gaps, because the map editor pins one by its number within its own type.")]
    public void ShouldNumberEveryDistributionScenarioContiguouslyFromOne()
    {
        Assert.Multiple(() =>
        {
            foreach (AddOrUpdateMapResourceTypeDto type in _types.Where(type => type.IsDistribution))
            {
                int[] scenarioNumbers = [.. type.DistributionScenarios.Select(scenario => scenario.ScenarioNumber).Order()];

                Assert.That(scenarioNumbers, Is.Not.Empty, $"{type.Code} is a Distribution with no scenarios.");
                Assert.That(scenarioNumbers, Is.EqualTo(Enumerable.Range(1, scenarioNumbers.Length).ToArray()), $"{type.Code} has a gap in its scenario numbers.");
            }
        });
    }

    [Test, Description("Every scenario names each Asset once in an amount above 0, since the validator rejects a duplicate Asset or an empty amount.")]
    public void ShouldGiveEveryScenarioDistinctAssetsInPositiveAmounts()
    {
        Assert.Multiple(() =>
        {
            foreach ((AddOrUpdateMapResourceTypeDto type, MapResourceDistributionScenarioDto scenario) in AllScenarios())
            {
                Assert.That(scenario.Amounts, Is.Not.Empty, $"{type.Code} scenario {scenario.ScenarioNumber} holds nothing.");
                Assert.That(scenario.Amounts.Select(amount => amount.AssetId), Is.Unique, $"{type.Code} scenario {scenario.ScenarioNumber} names an Asset twice.");

                foreach (MapResourceDistributionScenarioAmountDto amount in scenario.Amounts)
                {
                    Assert.That(amount.Amount, Is.GreaterThan(0), $"{type.Code} scenario {scenario.ScenarioNumber} holds {amount.Amount} of Asset {amount.AssetId}.");
                }
            }
        });
    }

    [Test, Description("Every scenario can be rolled somewhere, and weighs more than nothing wherever it can, since a scenario with no weight is dead data.")]
    public void ShouldGiveEveryScenarioAtLeastOneBiomeItCanAppearIn()
    {
        Assert.Multiple(() =>
        {
            foreach ((AddOrUpdateMapResourceTypeDto type, MapResourceDistributionScenarioDto scenario) in AllScenarios())
            {
                Assert.That(scenario.BiomeWeights, Is.Not.Empty, $"{type.Code} scenario {scenario.ScenarioNumber} ({scenario.DisplayName}) can never be rolled.");
                Assert.That(scenario.BiomeWeights.Select(weight => weight.BiomeType), Is.Unique, $"{type.Code} scenario {scenario.ScenarioNumber} weighs one Biome twice.");

                foreach (MapResourceDistributionScenarioBiomeWeightDto weight in scenario.BiomeWeights)
                {
                    Assert.That(weight.Weight, Is.GreaterThan(0), $"{type.Code} scenario {scenario.ScenarioNumber} weighs {weight.Weight} in {weight.BiomeType}.");
                }
            }
        });
    }

    [Test, Description("No scenario is weighted in a Biome whose ceiling its total exceeds, so an instance can never be created above the ceiling it then grows towards.")]
    public void ShouldNeverWeightAScenarioInABiomeItWouldNotFitIn()
    {
        Assert.Multiple(() =>
        {
            foreach ((AddOrUpdateMapResourceTypeDto type, MapResourceDistributionScenarioDto scenario) in AllScenarios())
            {
                int total = scenario.Amounts.Sum(amount => amount.Amount);

                foreach (MapResourceDistributionScenarioBiomeWeightDto weight in scenario.BiomeWeights)
                {
                    // The Geography-specific habitat with the lowest ceiling binds too: a Biome weight names no Geography, so a scenario weighted in a Biome
                    // can land on any tile of it.
                    int ceiling = MinimumCeilingIn(type, weight.BiomeType);

                    Assert.That(total, Is.LessThanOrEqualTo(ceiling), $"{type.Code} scenario {scenario.ScenarioNumber} ({scenario.DisplayName}) totals {total} but {weight.BiomeType} holds at most {ceiling}.");
                }
            }
        });
    }

    [Test, Description("Every Asset code the seed names is one the Asset seed really creates, so a renamed Asset is caught here rather than as a missing Asset at seed time.")]
    public void ShouldOnlyNameAssetsTheAssetSeedCreates()
    {
        string[] seededAssetCodes = [.. AllSeededAssetCodes()];

        Assert.Multiple(() =>
        {
            foreach (string code in MapResourceTypeSeedData.AssetCodes)
            {
                Assert.That(seededAssetCodes.Contains(code, StringComparer.OrdinalIgnoreCase), Is.True, $"The Asset seed creates no Asset with code '{code}'.");
            }
        });
    }

    [Test, Description("No seeded resource names Forage, which is the Biome's own food and would be eaten twice over by a patch of it sitting on the tile.")]
    public void ShouldNeverNameTheForageAsset()
    {
        Assert.That(MapResourceTypeSeedData.AssetCodes, Has.None.EqualTo(_FORAGE).IgnoreCase);
    }

    /// <summary>
    /// Every Distribution scenario across every seeded resource, carrying the type it belongs to so a failure names it.
    /// </summary>
    private IEnumerable<(AddOrUpdateMapResourceTypeDto Type, MapResourceDistributionScenarioDto Scenario)> AllScenarios()
    {
        return _types.SelectMany(type => type.DistributionScenarios.Select(scenario => (type, scenario)));
    }

    /// <summary>
    /// The lowest ceiling any of a type's habitats puts on a tile of one Biome, Geography-specific habitats included.
    /// </summary>
    private static int MinimumCeilingIn(AddOrUpdateMapResourceTypeDto type, BiomeType biomeType)
    {
        return type.Habitats
            .Where(habitat => habitat.BiomeType == biomeType)
            .Min(habitat => habitat.MaximumQuantity ?? 0);
    }

    /// <summary>
    /// The code every Asset in the Asset seed will be stored under, deriving the ones that leave it unset exactly as the upsert would.
    /// </summary>
    /// <remarks>
    /// The same walk as <c>AssetSeedDataTests</c> makes, duplicated rather than shared because the two fixtures are checking different things with it: that
    /// one that the codes are internally coherent, this one that they are the codes a MapResourceType can point at.
    /// </remarks>
    private static IEnumerable<string> AllSeededAssetCodes()
    {
        List<UpsertAssetCategoryDto> categories = [.. AssetSeedData.Build().SelectMany(metaCategory => metaCategory.Categories)];

        IEnumerable<string> groupedCodes = categories
            .SelectMany(category => category.Groups)
            .SelectMany(group => group.Assets.Select(asset => asset.Code ?? $"{group.Code}.{ProperCode(asset.DisplayName)}"));

        IEnumerable<string> standaloneCodes = categories
            .SelectMany(category => category.Assets.Select(asset => asset.Code ?? $"{category.Code}.{ProperCode(asset.DisplayName)}"));

        return groupedCodes.Concat(standaloneCodes);
    }

    /// <summary>
    /// Mirrors the Asset upsert's code derivation, so a derived code is compared as the one that will really be stored.
    /// </summary>
    private static string ProperCode(string name)
    {
        return string.Concat(
            System.Text.RegularExpressions.Regex.Split(name.Trim(), "[^a-zA-Z]+")
                .Where(word => word.Length > 0)
                .Select(word => char.ToUpperInvariant(word[0]) + word[1..].ToLowerInvariant()));
    }
}
