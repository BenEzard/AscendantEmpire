namespace MapGenerationPlay.Noise;

/// <summary>
/// Tileable fBm: produces noise that wraps seamlessly in X and/or Y.
///
/// Technique: bilinear blending of shifted samples.
/// For a noise that tiles with period (Wx, Wy), we sample four positions:
///   s00 = noise(x,      y)
///   s10 = noise(x - Wx, y)
///   s01 = noise(x,      y - Wy)
///   s11 = noise(x - Wx, y - Wy)
/// and blend them by the fractional position within [0, Wx] × [0, Wy] using
/// the smooth fade function. At x=0 and x=Wx the blend weight is 0 or 1, so
/// the two sides match exactly — the seam is invisible.
///
/// This is applied per-octave inside the fBm loop so every frequency level tiles,
/// not just the base.
/// </summary>
public sealed class TileableFbm
{
    private readonly PerlinNoise _noise;

    /// <summary>Width of the tile in world units.</summary>
    public int TileWidth { get; init; }

    /// <summary>Height of the tile in world units.</summary>
    public int TileHeight { get; init; }

    /// <summary>If false, the noise does NOT wrap horizontally (plain fBm in X).</summary>
    public bool WrapX { get; init; } = true;

    /// <summary>If false, the noise does NOT wrap vertically (plain fBm in Y).</summary>
    public bool WrapY { get; init; } = true;

    public int Octaves { get; init; } = 6;
    public double Persistence { get; init; } = 0.5;
    public double Lacunarity { get; init; } = 2.0;
    public double Scale { get; init; } = 1.0;

    public TileableFbm(PerlinNoise noise, int tileWidth, int tileHeight)
    {
        _noise     = noise;
        TileWidth  = tileWidth;
        TileHeight = tileHeight;
    }

    /// <summary>
    /// Samples tileable fBm at (x, y) where x ∈ [0, TileWidth) and y ∈ [0, TileHeight).
    /// Returns approximately [-1, 1].
    /// </summary>
    public double Sample(double x, double y)
    {
        double value     = 0;
        double amplitude = 1;
        double frequency = Scale;
        double maxValue  = 0;

        for (int i = 0; i < Octaves; i++)
        {
            // Noise-space coordinates for this octave.
            double nx = x * frequency;
            double ny = y * frequency;

            double sample;
            if (WrapX || WrapY)
            {
                // Period of the tile in noise space at this octave's frequency.
                double periodX = TileWidth  * frequency;
                double periodY = TileHeight * frequency;

                // Smooth blend weights: position within the current noise-space tile.
                double wx = WrapX ? Fade((nx % periodX + periodX) % periodX / periodX) : 0;
                double wy = WrapY ? Fade((ny % periodY + periodY) % periodY / periodY) : 0;

                // Four samples (we only need the shifted ones when wrapping that axis).
                double s00 = _noise.Sample(nx,          ny         );
                double s10 = WrapX ? _noise.Sample(nx - periodX, ny         ) : s00;
                double s01 = WrapY ? _noise.Sample(nx,          ny - periodY) : s00;
                double s11 = (WrapX && WrapY) ? _noise.Sample(nx - periodX, ny - periodY) : (WrapX ? s10 : s01);

                // Bilinear blend.
                sample = Lerp(Lerp(s00, s10, wx), Lerp(s01, s11, wx), wy);
            }
            else
            {
                // Non-wrapping: plain Perlin sample.
                sample = _noise.Sample(nx, ny);
            }

            value    += sample * amplitude;
            maxValue += amplitude;
            amplitude *= Persistence;
            frequency *= Lacunarity;
        }

        return value / maxValue;
    }

    /// <summary>Quintic fade used for smooth blending weights.</summary>
    private static double Fade(double t) =>
        t * t * t * (t * (t * 6 - 15) + 10);

    private static double Lerp(double a, double b, double t) =>
        a + t * (b - a);
}
