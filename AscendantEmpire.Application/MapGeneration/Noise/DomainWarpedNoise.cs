namespace AscendantEmpire.Application.MapGeneration.Noise;

/// <summary>
/// Domain warping: distorts (x, y) coordinates using a second noise field before sampling
/// the terrain noise. Produces organically irregular continent shapes that avoid the
/// "upside-down bowl" symmetry of plain fBm.
///
/// Optional double-warp pass for greater turbulence. The magic offset constants (5.2, 1.3 etc.)
/// ensure the x and y displacement fields sample very different parts of the warp noise.
/// </summary>
public sealed class DomainWarpedNoise
{
    private readonly FractalBrownianMotion _primary;
    private readonly FractalBrownianMotion _warp;

    private readonly bool _doubleWarp;

    /// <summary>
    /// How far (in noise-space units) the warp can displace the sample point.
    /// Typical: 0.1–0.25. Above ~0.3 the result loses coherent structure.
    /// </summary>
    public double WarpStrength { get; init; } = 0.15;

    public DomainWarpedNoise(
        FractalBrownianMotion primary,
        PerlinNoise           warpNoise,
        bool                  doubleWarp = true)
    {
        _primary    = primary;
        _doubleWarp = doubleWarp;

        _warp = new FractalBrownianMotion(warpNoise)
        {
            Octaves     = primary.Octaves,
            Persistence = primary.Persistence,
            Lacunarity  = primary.Lacunarity,
            Scale       = primary.Scale,
        };
    }

    /// <summary>Returns approximately [-1, 1].</summary>
    public double Sample(double x, double y)
    {
        double wx1 = _warp.Sample(x + 0.0, y + 0.0) * WarpStrength;
        double wy1 = _warp.Sample(x + 5.2, y + 1.3) * WarpStrength;
        double px  = x + wx1;
        double py  = y + wy1;

        if (_doubleWarp)
        {
            double wx2 = _warp.Sample(px + 1.7, py + 9.2) * WarpStrength;
            double wy2 = _warp.Sample(px + 8.3, py + 2.8) * WarpStrength;
            px += wx2;
            py += wy2;
        }

        return _primary.Sample(px, py);
    }
}
