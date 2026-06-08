namespace AscendantEmpire.Application.MapGeneration.Noise;

/// <summary>
/// Ridged multifractal noise: sharp ridgelines for mountain ranges.
/// Uses |noise| inversion with feedback weighting. Output [0, 1] where 1 = ridge crest.
/// </summary>
public sealed class RidgedNoise
{
    private readonly PerlinNoise _noise;

    public int    Octaves    { get; init; } = 6;
    public double Lacunarity { get; init; } = 2.0;
    public double Gain       { get; init; } = 2.0;   // feedback strength — higher = sharper ridges
    public double Scale      { get; init; } = 1.0;

    public RidgedNoise(PerlinNoise noise) => _noise = noise;

    /// <summary>Returns [0, 1].</summary>
    public double Sample(double x, double y)
    {
        double value     = 0;
        double amplitude = 0.5;
        double frequency = Scale;
        double weight    = 1.0;
        double maxValue  = 0;

        for (int i = 0; i < Octaves; i++)
        {
            double signal = 1.0 - Math.Abs(_noise.Sample(x * frequency, y * frequency));
            signal *= signal;           // square to sharpen peaks
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
