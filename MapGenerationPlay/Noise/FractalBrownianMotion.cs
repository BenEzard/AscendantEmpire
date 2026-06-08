namespace MapGenerationPlay.Noise;

/// <summary>
/// Fractional Brownian Motion (fBm): combines multiple octaves of Perlin noise
/// to create self-similar, natural-looking terrain.
///
/// Each octave doubles the spatial frequency (lacunarity = 2) and halves the
/// contribution (persistence = 0.5), mimicking how real terrain has large-scale
/// mountains plus medium hills plus fine rocky detail.
///
/// Output is normalised to approximately [-1, 1].
/// </summary>
public sealed class FractalBrownianMotion
{
    private readonly PerlinNoise _noise;

    /// <summary>How many octaves to layer. More = finer detail, slower.</summary>
    public int Octaves { get; init; } = 6;

    /// <summary>
    /// Amplitude multiplier per octave (0..1).
    /// Lower values = smoother terrain (high frequencies contribute less).
    /// Typical: 0.5.
    /// </summary>
    public double Persistence { get; init; } = 0.5;

    /// <summary>
    /// Frequency multiplier per octave.
    /// Typical: 2.0 (each octave doubles the spatial frequency).
    /// </summary>
    public double Lacunarity { get; init; } = 2.0;

    /// <summary>
    /// Base frequency scale: higher values zoom out and see more of the noise field.
    /// Think of it as "how many noise cycles fit across the map width".
    /// </summary>
    public double Scale { get; init; } = 1.0;

    public FractalBrownianMotion(PerlinNoise noise) => _noise = noise;

    /// <summary>
    /// Samples the fBm field at (x, y) in normalised map coordinates [0, width] × [0, height].
    /// Returns a value in approximately [-1, 1].
    /// </summary>
    public double Sample(double x, double y)
    {
        double value    = 0;
        double amplitude = 1;
        double frequency = Scale;
        double maxValue  = 0;   // tracks theoretical maximum for normalisation

        for (int i = 0; i < Octaves; i++)
        {
            value    += _noise.Sample(x * frequency, y * frequency) * amplitude;
            maxValue += amplitude;
            amplitude *= Persistence;
            frequency *= Lacunarity;
        }

        // Divide by maxValue so the result stays in [-1, 1] regardless of octave count.
        return value / maxValue;
    }
}
