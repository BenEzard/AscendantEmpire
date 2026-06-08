namespace MapGenerationPlay.Noise;

/// <summary>
/// Classic Ken Perlin "improved" gradient noise (2002).
///
/// How it works:
///   1. Find the unit-square cell that contains (x, y) by taking the integer floor of each coordinate.
///   2. Compute the fractional offset of the point within that cell.
///   3. Look up a pseudo-random gradient vector for each of the four cell corners using a seeded
///      permutation table.
///   4. Dot each gradient with the vector from its corner to (x, y).
///   5. Smooth-interpolate the four dot products using the fade function 6t^5 - 15t^4 + 10t^3,
///      which has zero first and second derivatives at t=0 and t=1 — this removes the grid
///      artifacts that plagued earlier noise implementations.
///
/// Output range: approximately [-1, 1], though rarely reaching the extremes.
/// The distribution is roughly bell-shaped around 0.
/// </summary>
public sealed class PerlinNoise
{
    /// <summary>Doubled permutation table (512 entries) to avoid modulo on lookup.</summary>
    private readonly int[] _perm;

    /// <param name="seed">
    /// Determines the permutation table. The same seed always produces the same noise field.
    /// </param>
    public PerlinNoise(long seed)
    {
        // Start with the ordered sequence 0..255.
        int[] p = new int[256];
        for (int i = 0; i < 256; i++) { p[i] = i; }

        // Fisher-Yates shuffle driven by the seed.
        // We derive a 32-bit seed by XOR-folding the 64-bit value.
        var rng = new Random((int)(seed ^ (seed >> 32)));
        for (int i = 255; i > 0; i--)
        {
            int j = rng.Next(i + 1);
            (p[i], p[j]) = (p[j], p[i]);
        }

        // Duplicate the table so we can index _perm[x + y] without bounds checks.
        _perm = new int[512];
        for (int i = 0; i < 512; i++) { _perm[i] = p[i & 255]; }
    }

    /// <summary>
    /// Samples the noise field at position (x, y).
    /// Returns a value in approximately [-1, 1].
    /// </summary>
    public double Sample(double x, double y)
    {
        // Integer cell coordinates (masked to [0, 255] for permutation lookup).
        int xi = (int)Math.Floor(x) & 255;
        int yi = (int)Math.Floor(y) & 255;

        // Fractional position within the cell.
        double xf = x - Math.Floor(x);
        double yf = y - Math.Floor(y);

        // Smooth interpolation weights.
        double u = Fade(xf);
        double v = Fade(yf);

        // Gradient hashes for the four corners of this cell.
        int aa = _perm[_perm[xi    ] + yi    ];
        int ab = _perm[_perm[xi    ] + yi + 1];
        int ba = _perm[_perm[xi + 1] + yi    ];
        int bb = _perm[_perm[xi + 1] + yi + 1];

        // Dot product of gradient with distance, then interpolate.
        double x1 = Lerp(Grad(aa, xf,     yf    ),
                         Grad(ba, xf - 1, yf    ), u);
        double x2 = Lerp(Grad(ab, xf,     yf - 1),
                         Grad(bb, xf - 1, yf - 1), u);

        return Lerp(x1, x2, v);
    }

    /// <summary>
    /// Quintic fade: 6t^5 - 15t^4 + 10t^3.
    /// Zero first and second derivatives at t=0 and t=1 eliminate grid-line artifacts.
    /// </summary>
    private static double Fade(double t) =>
        t * t * t * (t * (t * 6 - 15) + 10);

    private static double Lerp(double a, double b, double t) =>
        a + t * (b - a);

    /// <summary>
    /// Maps a hash value to one of 8 gradient directions in 2D:
    /// (±1, ±1), (±1, 0), (0, ±1).
    /// Using 8 directions gives a more isotropic distribution than 4.
    /// </summary>
    private static double Grad(int hash, double x, double y) =>
        (hash & 7) switch
        {
            0 =>  x + y,
            1 => -x + y,
            2 =>  x - y,
            3 => -x - y,
            4 =>  x,
            5 => -x,
            6 =>  y,
            7 => -y,
            _ => 0   // unreachable but satisfies the compiler
        };
}
