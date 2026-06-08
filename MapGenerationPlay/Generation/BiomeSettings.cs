namespace MapGenerationPlay.Generation;

/// <summary>
/// Tunable parameters for the biome assignment pass.
/// </summary>
public sealed class BiomeSettings
{
    /// <summary>
    /// If true, the equator row has hotter temperatures, concentrating Jungle in the centre.
    /// </summary>
    public bool Equator { get; init; } = false;

    /// <summary>
    /// How much random noise (0–1 weight) blends into the latitude-based temperature.
    /// 0 = perfectly banded temperature; 1 = fully random.
    /// Typical: 0.35 (mostly latitude-driven but with realistic variation).
    /// </summary>
    public double TemperatureNoiseWeight { get; init; } = 0.35;

    /// <summary>
    /// How many passes of the cellular-automaton biome-clustering step to run.
    /// More passes = bigger, stickier biome regions.
    /// </summary>
    public int ClusteringPasses { get; init; } = 2;

    /// <summary>
    /// Minimum fraction of neighbours that must share a biome for a tile to be "nudged"
    /// toward that biome during clustering. Prevents clustering from eliminating diversity.
    /// </summary>
    public double ClusteringThreshold { get; init; } = 0.60;

    /// <summary>
    /// Biomes where CanBeElevated is false in the domain model.
    /// These biomes cannot be assigned Hilly or Mountainous geography.
    /// Water biomes are handled separately via the geography layer.
    /// </summary>
    public IReadOnlySet<BiomeType> CannotBeElevated { get; init; } =
        new HashSet<BiomeType> { BiomeType.Desert, BiomeType.Ice };
}
