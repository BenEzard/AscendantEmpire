using AscendantEmpire.Domain.Enums.MapResources;
using AscendantEmpire.FrontendContracts.MapResources.AddOrUpdateMapResourceType;

namespace AscendantEmpire.BackendContracts.MapResources.AddOrUpdateMapResourceType;

/// <inheritdoc cref="IAddOrUpdateMapResourceTypeDto" />
public record class AddOrUpdateMapResourceTypeDto : IAddOrUpdateMapResourceTypeDto
{
    #region General Properties
    /// <inheritdoc />
    public required string Code { get; init; }

    /// <inheritdoc />
    public required string DisplayName { get; init; }

    /// <inheritdoc />
    public required string MapResourceCategoryCode { get; init; }

    /// <inheritdoc />
    public string? IconPath { get; init; }
    #endregion

    #region Kind
    /// <inheritdoc />
    public required bool IsRenewable { get; init; }

    /// <inheritdoc />
    public required bool IsStratified { get; init; }

    /// <inheritdoc />
    public required bool IsDistribution { get; init; }
    #endregion

    #region Extraction
    /// <inheritdoc />
    public required ExtractionMethod ExtractionMethod { get; init; }

    /// <inheritdoc />
    public decimal? CaptureLikelihood { get; init; }

    /// <inheritdoc />
    public required bool RequiresResearchToExtract { get; init; }
    #endregion

    #region Discovery
    /// <inheritdoc />
    public required bool IsDiscoverableByDefault { get; init; }

    /// <inheritdoc />
    public required decimal DiscoveryLikelihood { get; init; }

    /// <inheritdoc />
    public decimal? EstimationFuzzyFactorMin { get; init; }

    /// <inheritdoc />
    public decimal? EstimationFuzzyFactorMax { get; init; }
    #endregion

    #region Exhaustion (renewable only)
    /// <inheritdoc />
    public int? SurvivalRounds { get; init; }

    /// <inheritdoc />
    public decimal? ReseedLikelihood { get; init; }
    #endregion

    // Concrete collections for model binding, exposed through the interface as their interface types - the same arrangement as PutCampaignMapStartPositionsDto.

    /// <inheritdoc cref="IAddOrUpdateMapResourceTypeDto.Yields" />
    public IReadOnlyCollection<MapResourceTypeYieldDto> Yields { get; init; } = [];

    /// <inheritdoc cref="IAddOrUpdateMapResourceTypeDto.Habitats" />
    public IReadOnlyCollection<MapResourceHabitatDto> Habitats { get; init; } = [];

    /// <inheritdoc cref="IAddOrUpdateMapResourceTypeDto.DistributionScenarios" />
    public IReadOnlyCollection<MapResourceDistributionScenarioDto> DistributionScenarios { get; init; } = [];

    /// <inheritdoc cref="IAddOrUpdateMapResourceTypeDto.Layers" />
    public IReadOnlyCollection<MapResourceTypeLayerDto> Layers { get; init; } = [];

    IReadOnlyCollection<IMapResourceTypeYieldDto> IAddOrUpdateMapResourceTypeDto.Yields => Yields;

    IReadOnlyCollection<IMapResourceHabitatDto> IAddOrUpdateMapResourceTypeDto.Habitats => Habitats;

    IReadOnlyCollection<IMapResourceDistributionScenarioDto> IAddOrUpdateMapResourceTypeDto.DistributionScenarios => DistributionScenarios;

    IReadOnlyCollection<IMapResourceTypeLayerDto> IAddOrUpdateMapResourceTypeDto.Layers => Layers;
}
