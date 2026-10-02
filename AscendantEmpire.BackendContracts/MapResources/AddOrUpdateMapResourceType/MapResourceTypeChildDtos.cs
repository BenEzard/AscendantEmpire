using AscendantEmpire.Domain.Enums.Maps;
using AscendantEmpire.FrontendContracts.MapResources.AddOrUpdateMapResourceType;

namespace AscendantEmpire.BackendContracts.MapResources.AddOrUpdateMapResourceType;

// The children of AddOrUpdateMapResourceTypeDto. Kept together because none of them is ever sent on its own.

/// <inheritdoc cref="IMapResourceTypeYieldDto" />
public record class MapResourceTypeYieldDto : IMapResourceTypeYieldDto
{
    /// <inheritdoc />
    public required int AssetId { get; init; }

    /// <inheritdoc />
    public required int AmountPerUnit { get; init; }

    /// <inheritdoc />
    public required bool IsEnabledByDefault { get; init; }
}

/// <inheritdoc cref="IMapResourceHabitatDto" />
public record class MapResourceHabitatDto : IMapResourceHabitatDto
{
    /// <inheritdoc />
    public required BiomeType BiomeType { get; init; }

    /// <inheritdoc />
    public CampaignMapGeographyType? GeographyType { get; init; }

    /// <inheritdoc />
    public int? InitialQuantityMin { get; init; }

    /// <inheritdoc />
    public int? InitialQuantityMax { get; init; }

    /// <inheritdoc />
    public int? MaximumQuantity { get; init; }

    /// <inheritdoc />
    public decimal? GrowthPercentage { get; init; }

    /// <inheritdoc />
    public decimal? SpreadLikelihood { get; init; }

    /// <inheritdoc />
    public int? SpreadLimitMin { get; init; }

    /// <inheritdoc />
    public int? SpreadLimitMax { get; init; }
}

/// <inheritdoc cref="IMapResourceDistributionScenarioDto" />
public record class MapResourceDistributionScenarioDto : IMapResourceDistributionScenarioDto
{
    /// <inheritdoc />
    public required int ScenarioNumber { get; init; }

    /// <inheritdoc />
    public string? DisplayName { get; init; }

    /// <inheritdoc cref="IMapResourceDistributionScenarioDto.Amounts" />
    public IReadOnlyCollection<MapResourceDistributionScenarioAmountDto> Amounts { get; init; } = [];

    /// <inheritdoc cref="IMapResourceDistributionScenarioDto.BiomeWeights" />
    public IReadOnlyCollection<MapResourceDistributionScenarioBiomeWeightDto> BiomeWeights { get; init; } = [];

    IReadOnlyCollection<IMapResourceDistributionScenarioAmountDto> IMapResourceDistributionScenarioDto.Amounts => Amounts;

    IReadOnlyCollection<IMapResourceDistributionScenarioBiomeWeightDto> IMapResourceDistributionScenarioDto.BiomeWeights => BiomeWeights;
}

/// <inheritdoc cref="IMapResourceDistributionScenarioAmountDto" />
public record class MapResourceDistributionScenarioAmountDto : IMapResourceDistributionScenarioAmountDto
{
    /// <inheritdoc />
    public required int AssetId { get; init; }

    /// <inheritdoc />
    public required int Amount { get; init; }
}

/// <inheritdoc cref="IMapResourceDistributionScenarioBiomeWeightDto" />
public record class MapResourceDistributionScenarioBiomeWeightDto : IMapResourceDistributionScenarioBiomeWeightDto
{
    /// <inheritdoc />
    public required BiomeType BiomeType { get; init; }

    /// <inheritdoc />
    public required int Weight { get; init; }
}

/// <inheritdoc cref="IMapResourceTypeLayerDto" />
public record class MapResourceTypeLayerDto : IMapResourceTypeLayerDto
{
    /// <inheritdoc />
    public required int LayerNumber { get; init; }

    /// <inheritdoc />
    public required int AssetId { get; init; }

    /// <inheritdoc />
    public required decimal EmptyLikelihood { get; init; }

    /// <inheritdoc />
    public required int QuantityMin { get; init; }

    /// <inheritdoc />
    public required int QuantityMax { get; init; }

    /// <inheritdoc />
    public decimal? DiscoveryLikelihood { get; init; }

    /// <inheritdoc />
    public int? RequiredMineLevel { get; init; }

    /// <inheritdoc />
    public required decimal EstimationFuzzyFactorMin { get; init; }

    /// <inheritdoc />
    public required decimal EstimationFuzzyFactorMax { get; init; }
}
