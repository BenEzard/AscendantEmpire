namespace MapGenerationPlay.Generation;

/// <summary>
/// Tunable parameters for the geography generation pass.
/// All ratios are in percent (0–100).
/// </summary>
public sealed class GeographySettings
{
    /// <summary>Percentage of tiles that should be water (DeepWater + ShallowWater).</summary>
    public double WaterRatio { get; init; } = 55;

    /// <summary>Percentage of tiles that should be elevated (Hilly + Mountainous) — applied to all tiles, not just land.</summary>
    public double HillAndMountainRatio { get; init; } = 25;

    /// <summary>
    /// Number of water tiles away from the land/water boundary that count as ShallowWater.
    /// Represents the continental shelf depth.
    /// </summary>
    public int ShallowDepth { get; init; } = 3;

    /// <summary>
    /// If true, generate polar seas at the top and bottom rows by depressing terrain height near the edges.
    /// </summary>
    public bool Poles { get; init; } = true;

    /// <summary>
    /// How far from the pole edge (as a fraction of half the map height) the polar depression reaches.
    /// 0.3 means the depression affects the outermost 30% of rows from each pole.
    /// </summary>
    public double PolarReach { get; init; } = 0.30;

    /// <summary>
    /// Maximum height subtracted at the pole edge itself.
    /// Should be large enough to push pole tiles below the water threshold.
    /// </summary>
    public double PolarDepth { get; init; } = 0.60;

    /// <summary>
    /// Heightmap value above which ridged noise begins to influence the terrain.
    /// Keeps valleys and plains smooth while sharpening high terrain into ridgelines.
    /// </summary>
    public double RidgeInfluenceThreshold { get; init; } = 0.55;

    /// <summary>
    /// How strongly the ridged noise reshapes high terrain (0 = no effect, 1 = fully ridged).
    /// </summary>
    public double RidgeStrength { get; init; } = 0.60;

    /// <summary>
    /// Probability (0–1) that a qualifying ShallowWater tile on a steep coast becomes DeepWater,
    /// representing a deep harbour or cliff-edged inlet.
    /// </summary>
    public double HarbourChance { get; init; } = 0.08;
}
