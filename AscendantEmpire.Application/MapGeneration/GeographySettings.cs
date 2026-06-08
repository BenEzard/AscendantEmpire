namespace AscendantEmpire.Application.MapGeneration;

/// <summary>Parameters for the geography generation pass.</summary>
public sealed class GeographySettings
{
    public double WaterRatio            { get; init; } = 55;
    public double HillAndMountainRatio  { get; init; } = 25;
    public int    ShallowDepth          { get; init; } = 3;
    public bool   Poles                 { get; init; } = true;
    public double PolarReach            { get; init; } = 0.30;
    public double PolarDepth            { get; init; } = 0.60;
    public double RidgeInfluenceThreshold { get; init; } = 0.55;
    public double RidgeStrength         { get; init; } = 0.60;
    public double HarbourChance         { get; init; } = 0.08;
}
