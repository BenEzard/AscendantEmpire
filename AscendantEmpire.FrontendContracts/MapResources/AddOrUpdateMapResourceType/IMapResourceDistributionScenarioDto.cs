using AscendantEmpire.Domain.Enums.Maps;

namespace AscendantEmpire.FrontendContracts.MapResources.AddOrUpdateMapResourceType;

/// <summary>
/// One set of starting amounts a Distribution can be created from, and how likely it is to be picked in each Biome.
/// </summary>
public interface IMapResourceDistributionScenarioDto
{
    /// <summary>
    /// The scenario's number within its type, from 1. Unique per type.
    /// </summary>
    int ScenarioNumber { get; }

    /// <summary>
    /// An optional name for the map editor.
    /// </summary>
    string? DisplayName { get; }

    /// <summary>
    /// The starting amount of each Resource. At least one, each Resource at most once.
    /// </summary>
    IReadOnlyCollection<IMapResourceDistributionScenarioAmountDto> Amounts { get; }

    /// <summary>
    /// The scenario's weight in each Biome it can be picked in at random. Each Biome at most once. A scenario with none can still be chosen in the map editor.
    /// </summary>
    IReadOnlyCollection<IMapResourceDistributionScenarioBiomeWeightDto> BiomeWeights { get; }
}

/// <summary>
/// The starting amount of one Resource in a Distribution scenario.
/// </summary>
public interface IMapResourceDistributionScenarioAmountDto
{
    int ResourceId { get; }

    /// <summary>
    /// The starting amount, as a whole number. Above 0.
    /// </summary>
    int Amount { get; }
}

/// <summary>
/// A Distribution scenario's weight in one Biome, relative to the type's other scenarios there.
/// </summary>
public interface IMapResourceDistributionScenarioBiomeWeightDto
{
    BiomeType BiomeType { get; }

    /// <summary>
    /// The weight. Above 0.
    /// </summary>
    int Weight { get; }
}
