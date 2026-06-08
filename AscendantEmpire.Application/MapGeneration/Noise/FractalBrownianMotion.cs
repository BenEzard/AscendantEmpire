namespace AscendantEmpire.Application.MapGeneration.Noise;

/// <summary>
/// Fractional Brownian Motion: layers multiple octaves of Perlin noise for self-similar,
/// natural-looking terrain. Output is normalised to approximately [-1, 1].
/// </summary>
public sealed class FractalBrownianMotion
{
    private readonly PerlinNoise _noise;

    public int    Octaves     { get; init; } = 6;
    public double Persistence { get; init; } = 0.5;   // amplitude decay per octave
    public double Lacunarity  { get; init; } = 2.0;   // frequency growth per octave
    public double Scale       { get; init; } = 1.0;   // base frequency

    public FractalBrownianMotion(PerlinNoise noise) => _noise = noise;

    /// <summary>Returns approximately [-1, 1].</summary>
    public double Sample(double x, double y)
    {
        double value     = 0;
        double amplitude = 1;
        double frequency = Scale;
        double maxValue  = 0;
        for (int i = 0; i < Octaves; i++)
        {
            value    += _noise.Sample(x * frequency, y * frequency) * amplitude;
            maxValue += amplitude;
            amplitude *= Persistence;
            frequency *= Lacunarity;
        }
        return value / maxValue;
    }
}
