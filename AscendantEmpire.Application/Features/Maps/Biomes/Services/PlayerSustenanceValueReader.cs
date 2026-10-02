using AscendantEmpire.Application.Data;
using AscendantEmpire.Application.Features.Maps.Biomes.Classes;
using AscendantEmpire.Application.Helpers.ScaledNumerics;
using AscendantEmpire.Domain.Enums.Maps;
using AscendantEmpire.Domain.Enums.Maps;

using Microsoft.EntityFrameworkCore;

namespace AscendantEmpire.Application.Features.Maps.Biomes.Services;

/// <inheritdoc cref="IPlayerSustenanceValueReader" />
internal sealed class PlayerSustenanceValueReader : IPlayerSustenanceValueReader
{
    /// <summary>
    /// Every sustenance resource, in declaration order.
    /// </summary>
    private static readonly SustenanceResource[] _SUSTENANCE_RESOURCES = Enum.GetValues<SustenanceResource>();

    public async Task<PlayerSustenanceValues> GetValuesAsync(AscendantEmpireContext context, int playerId, CancellationToken cancelToken = default)
    {
        // Only the four sustenance columns are needed off each biome. A projection, so nothing is tracked.
        List<BiomeBaseValues> biomes = await context.CampaignBiomes
            .Select(biome => new BiomeBaseValues
            {
                BiomeType = biome.BiomeTypeId,
                BaseForageValue = biome.BaseForageValue,
                BaseHuntValue = biome.BaseHuntValue,
                BaseFishValue = biome.BaseFishValue,
                BaseFodderValue = biome.BaseFodderValue
            })
            .ToListAsync(cancelToken);

        // A Player holds a handful of modifiers, so they are summed in memory below rather than grouped in SQL. That also keeps the "all biomes" rows simple to apply:
        // one row counts towards every biome.
        List<ModifierAmounts> modifiers = await context.PlayerSustenanceModifiers
            .Where(modifier => modifier.PlayerId == playerId)
            .Select(modifier => new ModifierAmounts
            {
                Resource = modifier.Resource,
                BiomeType = modifier.BiomeTypeId,
                FlatValue = modifier.FlatValue,
                PercentValue = modifier.PercentValue
            })
            .ToListAsync(cancelToken);

        var values = new Dictionary<(BiomeType Biome, SustenanceResource Resource), ScaledAmount?>(biomes.Count * _SUSTENANCE_RESOURCES.Length);

        foreach (BiomeBaseValues biome in biomes)
        {
            foreach (SustenanceResource resource in _SUSTENANCE_RESOURCES)
            {
                long? baseRawValue = biome.GetBaseValue(resource);

                long flatRawTotal = 0;
                long percentRawTotal = 0;

                foreach (ModifierAmounts modifier in modifiers)
                {
                    // A modifier counts when it names this resource, and either this biome or no biome (every biome).
                    bool appliesToThisValue = modifier.Resource == resource && (modifier.BiomeType is null || modifier.BiomeType == biome.BiomeType);

                    if (!appliesToThisValue)
                    {
                        continue;
                    }

                    checked
                    {
                        flatRawTotal += modifier.FlatValue;
                        percentRawTotal += modifier.PercentValue;
                    }
                }

                ScaledAmount? baseValue = baseRawValue is null ? null : ScaledAmount.FromRaw(baseRawValue.Value);

                values[(biome.BiomeType, resource)] = SustenanceValueCalculator.CalculateEffectiveValue(baseValue, ScaledAmount.FromRaw(flatRawTotal), ScaledPercent.FromRaw(percentRawTotal));
            }
        }

        return new PlayerSustenanceValues(playerId, values);
    }

    /// <summary>
    /// A biome's four base sustenance values, each stored scaled by <c>ScaledAmount.Scale</c>, or <see langword="null"/> where the biome does not have that resource.
    /// </summary>
    private sealed class BiomeBaseValues
    {
        public BiomeType BiomeType { get; init; }
        public long? BaseForageValue { get; init; }
        public long? BaseHuntValue { get; init; }
        public long? BaseFishValue { get; init; }
        public long? BaseFodderValue { get; init; }

        /// <summary>
        /// The base value column that holds the given resource.
        /// </summary>
        public long? GetBaseValue(SustenanceResource resource)
        {
            return resource switch
            {
                SustenanceResource.Forage => BaseForageValue,
                SustenanceResource.Hunt => BaseHuntValue,
                SustenanceResource.Fish => BaseFishValue,
                SustenanceResource.Fodder => BaseFodderValue,
                _ => throw new ArgumentOutOfRangeException(nameof(resource), resource, "Not a sustenance resource.")
            };
        }
    }

    /// <summary>
    /// The parts of one modifier the formula needs.
    /// </summary>
    private sealed class ModifierAmounts
    {
        public SustenanceResource Resource { get; init; }
        public BiomeType? BiomeType { get; init; }
        public long FlatValue { get; init; }
        public long PercentValue { get; init; }
    }
}
