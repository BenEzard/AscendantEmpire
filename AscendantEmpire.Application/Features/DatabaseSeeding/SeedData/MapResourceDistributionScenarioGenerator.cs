using AscendantEmpire.BackendContracts.MapResources.AddOrUpdateMapResourceType;
using AscendantEmpire.Domain.Enums.Maps;

namespace AscendantEmpire.Application.Features.DatabaseSeeding.SeedData;

/// <summary>
/// Generates a Distribution MapResourceType's scenarios from a far smaller description of them: a composition <i>archetype</i> crossed with a quantity
/// <i>band</i>, one scenario per pair.
/// </summary>
/// <remarks>
/// <para>
/// Shared by every seeded Distribution, because the useful thing to write down is not a list of scenarios but the shapes a tile's contents can take and the
/// sizes it comes in. Dozens of scenarios then fall out of a few lines, and retuning how often a rare Asset turns up is one number rather than an edit
/// across all of them. A stratified or single-quantity resource does not use this at all.
/// </para>
/// <para>
/// Amounts and totals are <b>logical</b> units throughout. The catalogue mapper scales them by <c>ScaledAmount.Scale</c> (1,000) on the way into the
/// database.
/// </para>
/// </remarks>
internal static class MapResourceDistributionScenarioGenerator
{
    /// <summary>
    /// How many scenarios <see cref="Generate"/> would produce from the same sets, for a resource's item count.
    /// </summary>
    public static int CountScenarios(params ScenarioSet[] sets)
    {
        return sets.Sum(set => set.Archetypes.Length * set.Bands.Length);
    }

    /// <summary>
    /// Every scenario the sets describe, numbered from 1 in the order the sets are given.
    /// </summary>
    /// <remarks>
    /// <b>The numbering is append-only.</b> The map editor pins a scenario on a tile by its <c>ScenarioNumber</c>, so inserting an archetype or a band in
    /// the middle of a table would silently re-point every pin past it. New archetypes go on the end of their table, new bands on the end of theirs, and a
    /// new set on the end of the list.
    /// </remarks>
    /// <param name="assetIdsByCode">Every Asset code the archetypes name, resolved to its id.</param>
    /// <param name="habitats">Where the resource grows. The ceiling in each is what decides which bands that Biome can hold.</param>
    /// <param name="sets">The archetype and band tables to cross. Several, where one group of archetypes belongs to different Biomes from another.</param>
    public static IEnumerable<MapResourceDistributionScenarioDto> Generate(
        IReadOnlyDictionary<string, int> assetIdsByCode,
        IReadOnlyCollection<SeededHabitat> habitats,
        params ScenarioSet[] sets)
    {
        int scenarioNumber = 1;

        foreach (ScenarioSet set in sets)
        {
            // Archetype-major, so one archetype's sizes sit together in the numbering.
            foreach (CompositionArchetype archetype in set.Archetypes)
            {
                foreach (QuantityBand band in set.Bands)
                {
                    yield return BuildScenario(assetIdsByCode, habitats, archetype, band, scenarioNumber);

                    scenarioNumber++;
                }
            }
        }
    }

    private static MapResourceDistributionScenarioDto BuildScenario(
        IReadOnlyDictionary<string, int> assetIdsByCode,
        IReadOnlyCollection<SeededHabitat> habitats,
        CompositionArchetype archetype,
        QuantityBand band,
        int scenarioNumber)
    {
        return new MapResourceDistributionScenarioDto
        {
            ScenarioNumber = scenarioNumber,
            DisplayName = $"{archetype.DisplayName}, {band.DisplayName}",
            Amounts = [.. AmountsFor(assetIdsByCode, archetype, band)],
            BiomeWeights = [.. BiomeWeightsFor(habitats, archetype, band)]
        };
    }

    /// <summary>
    /// Turns an archetype's percentage shares into whole amounts of each Asset, summing to exactly the band's total.
    /// </summary>
    /// <remarks>
    /// The largest share takes up whatever the division leaves over. A band total that is a multiple of 100 leaves nothing to take up - but a band need not
    /// be, and a scenario whose parts did not sum to its stated total would be a confusing thing to debug.
    /// </remarks>
    private static IEnumerable<MapResourceDistributionScenarioAmountDto> AmountsFor(
        IReadOnlyDictionary<string, int> assetIdsByCode,
        CompositionArchetype archetype,
        QuantityBand band)
    {
        int[] amounts = [.. archetype.Shares.Select(share => share.Share * band.Total / 100)];

        int largestAmountIndex = Array.IndexOf(amounts, amounts.Max());

        amounts[largestAmountIndex] += band.Total - amounts.Sum();

        return archetype.Shares.Select((share, index) => new MapResourceDistributionScenarioAmountDto
        {
            AssetId = assetIdsByCode[share.AssetCode],
            Amount = amounts[index]
        });
    }

    /// <summary>
    /// How likely this archetype at this size is in each Biome the band covers: the archetype's rarity scaled by the band's weight there.
    /// </summary>
    /// <remarks>
    /// A Biome whose ceiling the band's total exceeds gets no weight at all, so a scenario can never be rolled onto a tile it would not fit on. Since the
    /// habitats carrying those ceilings are the resource's own, the generated set cannot contradict them.
    /// </remarks>
    private static IEnumerable<MapResourceDistributionScenarioBiomeWeightDto> BiomeWeightsFor(
        IReadOnlyCollection<SeededHabitat> habitats,
        CompositionArchetype archetype,
        QuantityBand band)
    {
        return band.Weights
            .Where(weight => band.Total <= MaximumQuantityIn(habitats, weight.BiomeType))
            .Select(weight => new MapResourceDistributionScenarioBiomeWeightDto
            {
                BiomeType = weight.BiomeType,
                Weight = archetype.Rarity * weight.Weight
            });
    }

    /// <summary>
    /// The ceiling on a tile's total in one Biome, or 0 for a Biome the resource does not grow in.
    /// </summary>
    private static int MaximumQuantityIn(IReadOnlyCollection<SeededHabitat> habitats, BiomeType biomeType)
    {
        return habitats.SingleOrDefault(habitat => habitat.BiomeType == biomeType)?.MaximumQuantity ?? 0;
    }
}

/// <summary>
/// One Biome a seeded resource grows in, and its values on every tile of that Biome.
/// </summary>
internal sealed record SeededHabitat(BiomeType BiomeType, int MaximumQuantity, decimal GrowthPercentage)
{
    /// <summary>
    /// The habitat as the catalogue contract wants it.
    /// </summary>
    /// <remarks>
    /// A Distribution starts at its scenario's amounts and never spreads, so the initial-quantity range and all three spread values stay empty; the
    /// MapResourceType validator rejects a Distribution habitat that sets any of them.
    /// </remarks>
    public MapResourceHabitatDto ToDto()
    {
        return new MapResourceHabitatDto
        {
            BiomeType = BiomeType,
            GeographyType = null,
            MaximumQuantity = MaximumQuantity,
            GrowthPercentage = GrowthPercentage
        };
    }
}

/// <summary>
/// One composition shape: how much of a tile's total each Asset takes, and how common that shape is relative to the others.
/// </summary>
/// <param name="Rarity">How often this shape is chosen, relative to the others in its set. Scaled by the band's weight in each Biome.</param>
/// <param name="Shares">Each Asset's percentage of the total. Sums to 100, and names each Asset once.</param>
internal sealed record CompositionArchetype(string DisplayName, int Rarity, (string AssetCode, int Share)[] Shares);

/// <summary>
/// One starting size, and how likely that size is in each Biome it is offered to.
/// </summary>
/// <param name="Weights">The Biomes this size can appear in, each with its weight relative to the other sizes in that same Biome.</param>
internal sealed record QuantityBand(string DisplayName, int Total, (BiomeType BiomeType, int Weight)[] Weights);

/// <summary>
/// One group of composition shapes and the sizes they come in.
/// </summary>
/// <remarks>
/// A resource needs more than one set when a group of shapes belongs to different Biomes from another - wood holding bamboo only in jungle, and the other
/// woods only outside it - since the Biomes a scenario can appear in come from its band.
/// </remarks>
internal sealed record ScenarioSet(CompositionArchetype[] Archetypes, QuantityBand[] Bands);
