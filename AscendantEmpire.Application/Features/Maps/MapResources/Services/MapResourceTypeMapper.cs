using AscendantEmpire.Application.Helpers.ScaledNumerics;
using AscendantEmpire.BackendContracts.MapResources.AddOrUpdateMapResourceType;
using AscendantEmpire.BackendContracts.MapResources.GetMapResourceCatalogue;
using AscendantEmpire.Domain.Entities.Maps.MapResources;
using AscendantEmpire.Domain.Enums.Maps;
using AscendantEmpire.FrontendContracts.MapResources.AddOrUpdateMapResourceType;

namespace AscendantEmpire.Application.Features.Maps.MapResources.Services;

/// <summary>
/// Converts a <see cref="MapResourceType"/> and everything it owns to and from the shape the MapResource Type Manager edits.
/// </summary>
/// <remarks>
/// <para>
/// Quantities cross as whole numbers, as the Biome Manager's do, and are stored scaled by <see cref="ScaledAmount.Scale"/>. Likelihoods and percentages are fractions on
/// both sides.
/// </para>
/// <para>
/// Children are updated in place, matched on their natural keys, rather than cleared and re-added. That is not only tidier: yields and layers have composite keys, and
/// EF cannot track a deleted row and a new row with the same key at once; and a Distribution scenario keeps its id, which map placements and game instances point at.
/// </para>
/// </remarks>
internal static class MapResourceTypeMapper
{
    /// <summary>
    /// The type as the manager loads it. The type must have been read with all of its children, and its category.
    /// </summary>
    public static MapResourceTypeDetail ToDetail(MapResourceType type)
    {
        return new MapResourceTypeDetail
        {
            Id = type.Id,
            Code = type.Code,
            DisplayName = type.DisplayName,
            MapResourceCategoryCode = type.Category.Code,
            SortOrder = type.SortOrder,
            IconPath = type.IconPath,
            IsRenewable = type.IsRenewable,
            IsStratified = type.IsStratified,
            IsDistribution = type.IsDistribution,
            IsWaterResource = type.IsWaterResource,
            ExtractionMethod = type.ExtractionMethod,
            CaptureLikelihood = type.CaptureLikelihood,
            RequiresResearchToExtract = type.RequiresResearchToExtract,
            IsDiscoverableByDefault = type.IsDiscoverableByDefault,
            DiscoveryLikelihood = type.DiscoveryLikelihood,
            EstimationFuzzyFactorMin = type.EstimationFuzzyFactorMin,
            EstimationFuzzyFactorMax = type.EstimationFuzzyFactorMax,
            SurvivalRounds = type.SurvivalRounds,
            ReseedLikelihood = type.ReseedLikelihood,

            Yields = [.. type.Yields
                .OrderBy(y => y.AssetId)
                .Select(y => new MapResourceTypeYieldDto
                {
                    AssetId = y.AssetId,
                    AmountPerUnit = ToWhole(y.AmountPerUnit),
                    IsEnabledByDefault = y.IsEnabledByDefault
                })],

            // Biome-wide habitats before the Geography-specific ones in the same Biome, which is how they read.
            Habitats = [.. type.Habitats
                .OrderBy(h => h.BiomeTypeId)
                .ThenBy(h => h.CampaignMapGeographyTypeId.HasValue)
                .ThenBy(h => h.CampaignMapGeographyTypeId)
                .Select(h => new MapResourceHabitatDto
                {
                    BiomeType = h.BiomeTypeId,
                    GeographyType = h.CampaignMapGeographyTypeId,
                    InitialQuantityMin = ScaledResourceValue.ToWhole(h.InitialQuantityMin),
                    InitialQuantityMax = ScaledResourceValue.ToWhole(h.InitialQuantityMax),
                    MaximumQuantity = ScaledResourceValue.ToWhole(h.MaximumQuantity),
                    GrowthPercentage = h.GrowthPercentage,
                    SpreadLikelihood = h.SpreadLikelihood,
                    SpreadLimitMin = h.SpreadLimitMin,
                    SpreadLimitMax = h.SpreadLimitMax
                })],

            DistributionScenarios = [.. type.DistributionScenarios
                .OrderBy(s => s.ScenarioNumber)
                .Select(s => new MapResourceDistributionScenarioDto
                {
                    ScenarioNumber = s.ScenarioNumber,
                    DisplayName = s.DisplayName,
                    Amounts = [.. s.Amounts
                        .OrderBy(a => a.AssetId)
                        .Select(a => new MapResourceDistributionScenarioAmountDto { AssetId = a.AssetId, Amount = ToWhole(a.Amount) })],
                    BiomeWeights = [.. s.BiomeWeights
                        .OrderBy(w => w.BiomeTypeId)
                        .Select(w => new MapResourceDistributionScenarioBiomeWeightDto { BiomeType = w.BiomeTypeId, Weight = w.Weight })]
                })],

            Layers = [.. type.Layers
                .OrderBy(l => l.LayerNumber)
                .Select(l => new MapResourceTypeLayerDto
                {
                    LayerNumber = l.LayerNumber,
                    AssetId = l.AssetId,
                    EmptyLikelihood = l.EmptyLikelihood,
                    QuantityMin = ToWhole(l.QuantityMin),
                    QuantityMax = ToWhole(l.QuantityMax),
                    DiscoveryLikelihood = l.DiscoveryLikelihood,
                    RequiredMineLevel = l.RequiredMineLevel,
                    EstimationFuzzyFactorMin = l.EstimationFuzzyFactorMin,
                    EstimationFuzzyFactorMax = l.EstimationFuzzyFactorMax
                })]
        };
    }

    /// <summary>
    /// Writes <paramref name="dto"/> onto <paramref name="type"/>, a new entity or an existing one read with all of its children. Does not save.
    /// </summary>
    /// <param name="dto">The validated values.</param>
    /// <param name="type">The entity to update.</param>
    /// <param name="category">The category the type is in, which may itself be new in this save.</param>
    public static void Apply(IAddOrUpdateMapResourceTypeDto dto, MapResourceType type, MapResourceCategory category)
    {
        type.Code = dto.Code;
        type.DisplayName = dto.DisplayName;
        type.Category = category;

        // SortOrder is deliberately absent: the manager does not edit it, so an update must leave whatever the database holds. The handler sets it once, when it
        // creates the type.
        type.IconPath = string.IsNullOrWhiteSpace(dto.IconPath) ? null : dto.IconPath.Trim();
        type.IsRenewable = dto.IsRenewable;
        type.IsStratified = dto.IsStratified;
        type.IsDistribution = dto.IsDistribution;
        type.IsWaterResource = dto.IsWaterResource;
        type.ExtractionMethod = dto.ExtractionMethod;
        type.CaptureLikelihood = dto.CaptureLikelihood;
        type.RequiresResearchToExtract = dto.RequiresResearchToExtract;
        type.IsDiscoverableByDefault = dto.IsDiscoverableByDefault;
        type.DiscoveryLikelihood = dto.DiscoveryLikelihood;
        type.EstimationFuzzyFactorMin = dto.EstimationFuzzyFactorMin;
        type.EstimationFuzzyFactorMax = dto.EstimationFuzzyFactorMax;
        type.SurvivalRounds = dto.SurvivalRounds;
        type.ReseedLikelihood = dto.ReseedLikelihood;

        SyncChildren(
            type.Yields,
            dto.Yields,
            entity => entity.AssetId,
            item => item.AssetId,
            item => new MapResourceTypeYield { AssetId = item.AssetId },
            (item, entity) =>
            {
                entity.AmountPerUnit = ToRaw(item.AmountPerUnit);
                entity.IsEnabledByDefault = item.IsEnabledByDefault;
            });

        SyncChildren(
            type.Habitats,
            dto.Habitats,
            entity => (entity.BiomeTypeId, entity.CampaignMapGeographyTypeId),
            item => (item.BiomeType, item.GeographyType),
            item => new MapResourceHabitat { BiomeTypeId = item.BiomeType, CampaignMapGeographyTypeId = item.GeographyType },
            (item, entity) =>
            {
                entity.InitialQuantityMin = ScaledResourceValue.ToRaw(item.InitialQuantityMin);
                entity.InitialQuantityMax = ScaledResourceValue.ToRaw(item.InitialQuantityMax);
                entity.MaximumQuantity = ScaledResourceValue.ToRaw(item.MaximumQuantity);
                entity.GrowthPercentage = item.GrowthPercentage;
                entity.SpreadLikelihood = item.SpreadLikelihood;
                entity.SpreadLimitMin = item.SpreadLimitMin;
                entity.SpreadLimitMax = item.SpreadLimitMax;
            });

        SyncChildren(
            type.DistributionScenarios,
            dto.DistributionScenarios,
            entity => entity.ScenarioNumber,
            item => item.ScenarioNumber,
            item => new MapResourceDistributionScenario { ScenarioNumber = item.ScenarioNumber },
            (item, scenario) =>
            {
                scenario.DisplayName = string.IsNullOrWhiteSpace(item.DisplayName) ? null : item.DisplayName.Trim();

                SyncChildren(
                    scenario.Amounts,
                    item.Amounts,
                    entity => entity.AssetId,
                    amount => amount.AssetId,
                    amount => new MapResourceDistributionScenarioAmount { AssetId = amount.AssetId },
                    (amount, entity) => entity.Amount = ToRaw(amount.Amount));

                SyncChildren(
                    scenario.BiomeWeights,
                    item.BiomeWeights,
                    entity => entity.BiomeTypeId,
                    weight => weight.BiomeType,
                    weight => new MapResourceDistributionScenarioBiomeWeight { BiomeTypeId = weight.BiomeType },
                    (weight, entity) => entity.Weight = weight.Weight);
            });

        SyncChildren(
            type.Layers,
            dto.Layers,
            entity => entity.LayerNumber,
            item => item.LayerNumber,
            item => new MapResourceTypeLayer { LayerNumber = item.LayerNumber },
            (item, entity) =>
            {
                entity.AssetId = item.AssetId;
                entity.EmptyLikelihood = item.EmptyLikelihood;
                entity.QuantityMin = ToRaw(item.QuantityMin);
                entity.QuantityMax = ToRaw(item.QuantityMax);
                entity.DiscoveryLikelihood = item.DiscoveryLikelihood;
                entity.RequiredMineLevel = item.RequiredMineLevel;
                entity.EstimationFuzzyFactorMin = item.EstimationFuzzyFactorMin;
                entity.EstimationFuzzyFactorMax = item.EstimationFuzzyFactorMax;
            });
    }

    /// <summary>
    /// Makes <paramref name="existing"/> match <paramref name="desired"/>, matching on a key: rows whose key is no longer wanted are removed (and, being owned, deleted by
    /// EF), wanted keys with no row are created, and every wanted row is updated.
    /// </summary>
    private static void SyncChildren<TEntity, TItem, TKey>(
        ICollection<TEntity> existing,
        IEnumerable<TItem> desired,
        Func<TEntity, TKey> entityKey,
        Func<TItem, TKey> itemKey,
        Func<TItem, TEntity> create,
        Action<TItem, TEntity> update)
        where TKey : notnull
    {
        // The validator has already rejected duplicate keys, so the dictionary cannot throw.
        Dictionary<TKey, TItem> wanted = desired.ToDictionary(itemKey);

        foreach (TEntity stale in existing.Where(entity => wanted.ContainsKey(entityKey(entity)) == false).ToList())
        {
            existing.Remove(stale);
        }

        Dictionary<TKey, TEntity> kept = existing.ToDictionary(entityKey);

        foreach ((TKey key, TItem item) in wanted)
        {
            if (kept.TryGetValue(key, out TEntity? entity) == false)
            {
                entity = create(item);
                existing.Add(entity);
            }

            update(item, entity);
        }
    }

    private static long ToRaw(int whole)
    {
        return ScaledAmount.FromWhole(whole).RawValue;
    }

    private static int ToWhole(long raw)
    {
        return (int)ScaledAmount.FromRaw(raw).ToWholeRoundedDown();
    }
}
