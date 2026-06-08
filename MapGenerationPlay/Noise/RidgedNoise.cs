namespace MapGenerationPlay.Noise;

/// <summary>
/// Ridged multifractal noise: produces sharp ridgelines suitable for mountain ranges.
///
/// The trick: instead of summing octaves linearly, we take the absolute value of each
/// octave sample and invert it (1 - |v|), then weight higher octaves by the previous
/// octave's value. This creates a feedback loop that produces sharp peaks surrounded by
/// lower terrain — exactly the profile of a mountain ridge viewed from above.
///
/// Output range: [0, 1] where values near 1 are ridge peaks.
/// </summary>
public sealed class RidgedNoise
{
    private readonly PerlinNoise _noise;

    /// <summary>How many octaves to layer.</summary>
    public int Octaves { get; init; } = 6;

    /// <summary>Frequency multiplier per octave (typically 2.0).</summary>
    public double Lacunarity { get; init; } = 2.0;

    /// <summary>
    /// Controls how strongly the previous octave's value weights the next octave.
    /// Higher gain = sharper ridges. Typical: 2.0.
    /// </summary>
    public double Gain { get; init; } = 2.0;

    /// <summary>Base frequency scale.</summary>
    public double Scale { get; init; } = 1.0;

    public RidgedNoise(PerlinNoise noise) => _noise = noise;

    /// <summary>
    /// Samples ridged noise at (x, y). Returns a value in [0, 1] (1 = ridge crest).
    /// </summary>
    public double Sample(double x, double y)
    {
        double value     = 0;
        double amplitude = 0.5;
        double frequency = Scale;
        double weight    = 1.0;   // feedback from previous octave
        double maxValue  = 0;

        for (int i = 0; i < Octaves; i++)
        {
            // Raw noise in [-1, 1], folded to [0, 1] and inverted so ridges are peaks.
            double signal = 1.0 - Math.Abs(_noise.Sample(x * frequency, y * frequency));

            // Square the signal to sharpen ridges.
            signal *= signal;

            // The weight from the prior octave pulls subsequent octaves toward the ridgeline.
            signal *= weight;
            weight  = Math.Clamp(signal * Gain, 0, 1);

            value    += signal * amplitude;
            maxValue += amplitude;

            amplitude *= 0.5;
            frequency *= Lacunarity;
        }

        return value / maxValue;
    }
}
