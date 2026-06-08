namespace AscendantEmpire.Application.MapGeneration.Noise;

/// <summary>
/// Classic Ken Perlin "improved" gradient noise (2002).
/// Produces smooth, multi-scale noise values in approximately [-1, 1].
///
/// Algorithm:
///   1. Find the unit-square cell containing (x, y).
///   2. Compute fractional offset of the point within the cell.
///   3. Look up pseudo-random gradient vectors for each corner via a seeded permutation table.
///   4. Dot each gradient with the corner-to-point distance vector.
///   5. Smooth-interpolate using the quintic fade 6t^5 - 15t^4 + 10t^3 (zero 1st and 2nd derivatives at endpoints).
/// </summary>
public sealed class PerlinNoise
{
    private readonly int[] _perm;

    /// <param name="seed">Determines the permutation table. Same seed → same noise field.</param>
    public PerlinNoise(long seed)
    {
        int[] p = new int[256];
        for (int i = 0; i < 256; i++) { p[i] = i; }

        // Fisher-Yates shuffle driven by the seed.
        var rng = new Random((int)(seed ^ (seed >> 32)));
        for (int i = 255; i > 0; i--)
        {
            int j = rng.Next(i + 1);
            (p[i], p[j]) = (p[j], p[i]);
        }

        // Duplicate so we can index _perm[x + y] without bounds checks.
        _perm = new int[512];
        for (int i = 0; i < 512; i++) { _perm[i] = p[i & 255]; }
    }

    /// <summary>Samples the noise field at (x, y). Returns approximately [-1, 1].</summary>
    public double Sample(double x, double y)
    {
        int xi = (int)Math.Floor(x) & 255;
        int yi = (int)Math.Floor(y) & 255;
        double xf = x - Math.Floor(x);
        double yf = y - Math.Floor(y);
        double u = Fade(xf);
        double v = Fade(yf);

        int aa = _perm[_perm[xi    ] + yi    ];
        int ab = _perm[_perm[xi    ] + yi + 1];
        int ba = _perm[_perm[xi + 1] + yi    ];
        int bb = _perm[_perm[xi + 1] + yi + 1];

        double x1 = Lerp(Grad(aa, xf,     yf    ), Grad(ba, xf - 1, yf    ), u);
        double x2 = Lerp(Grad(ab, xf,     yf - 1), Grad(bb, xf - 1, yf - 1), u);
        return Lerp(x1, x2, v);
    }

    private static double Fade(double t) => t * t * t * (t * (t * 6 - 15) + 10);
    private static double Lerp(double a, double b, double t) => a + t * (b - a);

    // 8 gradient directions for better isotropy: (±1,±1), (±1,0), (0,±1).
    private static double Grad(int hash, double x, double y) =>
        (hash & 7) switch
        {
            0 =>  x + y, 1 => -x + y, 2 =>  x - y, 3 => -x - y,
            4 =>  x,     5 => -x,     6 =>  y,      7 => -y,
            _ => 0
        };
}
