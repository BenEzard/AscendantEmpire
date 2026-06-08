namespace MapGenerationPlay.Noise;

/// <summary>
/// Domain warping: before sampling the "real" noise field, distort the (x, y) input
/// coordinates using a second noise field. The result is noise that appears twisted
/// and organically irregular — ideal for continent shapes that aren't obviously circular.
///
/// Without domain warping, fBm noise tends to produce hill shapes that look like
/// upside-down bowls because the gradients flow smoothly outward. Warping the
/// coordinates before sampling breaks this symmetry.
///
/// Two levels of warping are available:
///   - Single warp: offset (x, y) once using a separate fbm instance.
///   - Double warp (recommended for continents): warp again using the already-warped
///     coordinates. This produces much more turbulent, geologically convincing shapes.
///
/// Warp strength controls how far the coordinates are displaced. Too low = barely any
/// effect; too high = smeared noise that loses all structure. A good starting range
/// is 0.5–2.0 times the map width per unit.
/// </summary>
public sealed class DomainWarpedNoise
{
    private readonly FractalBrownianMotion _primary;  // the terrain noise
    private readonly FractalBrownianMotion _warpX;    // displaces x
    private readonly FractalBrownianMotion _warpY;    // displaces y

    // Optional second warp pass for extra turbulence.
    private readonly FractalBrownianMotion? _warp2X;
    private readonly FractalBrownianMotion? _warp2Y;

    /// <summary>
    /// How many world-units the warp noise can displace the sample point.
    /// Larger = more twisted coastlines.
    /// </summary>
    public double WarpStrength { get; init; } = 1.0;

    /// <param name="primary">The noise field to sample (the terrain heightmap).</param>
    /// <param name="warpNoise">A different PerlinNoise instance (different seed) for the warp offset.</param>
    /// <param name="doubleWarp">Apply a second warp pass for more turbulence.</param>
    public DomainWarpedNoise(
        FractalBrownianMotion primary,
        PerlinNoise           warpNoise,
        bool                  doubleWarp = true)
    {
        _primary = primary;

        // The warp samplers use offset seeds baked into constant coordinate shifts,
        // because feeding the same (x,y) to both warpX and warpY would give identical
        // offsets (making the displacement purely diagonal). The constant shifts ensure
        // the x and y displacements are decorrelated from each other.
        _warpX = new FractalBrownianMotion(warpNoise)
        {
            Octaves     = primary.Octaves,
            Persistence = primary.Persistence,
            Lacunarity  = primary.Lacunarity,
            Scale       = primary.Scale,
        };
        _warpY = _warpX; // same instance, but we shift coordinates when sampling

        if (doubleWarp)
        {
            _warp2X = _warpX;
            _warp2Y = _warpX;
        }
    }

    /// <summary>
    /// Samples the domain-warped noise at (x, y).
    /// Returns approximately [-1, 1].
    /// </summary>
    public double Sample(double x, double y)
    {
        // First warp pass: compute displacement using offset coords so X and Y are decorrelated.
        // The magic constants (1.7, 9.2 etc.) are arbitrary irrational-ish offsets that
        // ensure the x-warp and y-warp samplers see very different parts of the noise field.
        double wx1 = _warpX.Sample(x + 0.0,  y + 0.0 ) * WarpStrength;
        double wy1 = _warpY.Sample(x + 5.2,  y + 1.3 ) * WarpStrength;

        double px = x + wx1;
        double py = y + wy1;

        if (_warp2X is not null)
        {
            // Second warp pass using the already-warped coordinates.
            double wx2 = _warp2X.Sample(px + 1.7, py + 9.2) * WarpStrength;
            double wy2 = _warp2Y!.Sample(px + 8.3, py + 2.8) * WarpStrength;
            px += wx2;
            py += wy2;
        }

        return _primary.Sample(px, py);
    }
}
