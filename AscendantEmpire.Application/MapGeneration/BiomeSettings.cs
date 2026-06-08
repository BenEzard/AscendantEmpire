using AscendantEmpire.Domain.Enums.Maps;

namespace AscendantEmpire.Application.MapGeneration;

/// <summary>Parameters for the biome assignment pass.</summary>
public sealed class BiomeSettings
{
    public bool   Equator                  { get; init; } = false;
    public double TemperatureNoiseWeight   { get; init; } = 0.35;
    public int    ClusteringPasses         { get; init; } = 2;
    public double ClusteringThreshold      { get; init; } = 0.60;

    /// <summary>Biomes that may not be placed on Hilly or Mountainous geography.</summary>
    public IReadOnlySet<BiomeType> CannotBeElevated { get; init; } = new HashSet<BiomeType> { BiomeType.Desert, BiomeType.Ice };
}
