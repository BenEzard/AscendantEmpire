using MapGenerationPlay.Noise;

namespace MapGenerationPlay.Generation;

/// <summary>
/// Converts a normalised heightmap [0, 1] into a grid of CampaignMap geography types.
///
/// Pipeline:
///   1. Apply polar altitude reduction (if poles=true) to push top/bottom rows underwater.
///   2. Blend a second ridged-noise layer to create mountain ranges rather than scattered peaks.
///   3. Threshold the blended height to classify each tile as water, flat, hilly, or mountainous.
///   4. Run a distance transform to mark the ring of land tiles nearest water as ShallowWater,
///      and leave deeper ocean as DeepWater.
///   5. (Harbour exception) Where the coastline gradient is very steep, occasionally allow
///      DeepWater to border land directly — representing cliff-edged deep harbours.
/// </summary>
public sealed class GeographyGenerator
{
    private readonly GeographySettings _settings;

    public GeographyGenerator(GeographySettings settings) => _settings = settings;

    /// <summary>
    /// Generates the geography grid from a pre-computed normalised heightmap.
    /// </summary>
    /// <param name="heightmap">
    /// Flat array [y * width + x] of values in [0, 1] produced by the noise generator.
    /// </param>
    /// <param name="width">Map width in tiles.</param>
    /// <param name="height">Map height in tiles.</param>
    /// <param name="ridgeNoise">
    /// A separate ridged-noise sampler (different seed offset) used to bias terrain
    /// toward forming connected mountain ranges.
    /// </param>
    /// <returns>Flat geography array, same indexing as heightmap.</returns>
    public GeographyType[] Generate(
        double[]    heightmap,
        int         width,
        int         height,
        RidgedNoise ridgeNoise)
    {
        // ── Step 1: Apply polar altitude reduction ────────────────────────────
        // Near the top and bottom edges, subtract a gradient from the height so those
        // tiles fall below the water threshold more readily, creating polar seas.
        double[] adjusted = (double[])heightmap.Clone();

        if (_settings.Poles)
        {
            for (int y = 0; y < height; y++)
            {
                // normalised distance from the nearest pole: 0 at edge, 1 at equator
                double distFromPole = Math.Min(y, height - 1 - y) / (double)(height / 2.0);

                // Strength of the polar depression: starts strong at the pole (distFromPole=0)
                // and fades to zero well before the equator.
                double polarStrength = Math.Max(0, 1.0 - distFromPole / _settings.PolarReach) * _settings.PolarDepth;

                for (int x = 0; x < width; x++)
                {
                    adjusted[y * width + x] -= polarStrength;
                }
            }
        }

        // ── Step 2: Blend ridged noise for mountain ranges ────────────────────
        // The base heightmap gives the large-scale layout of high vs low land.
        // Multiplying in a ridged noise layer concentrates the highest values into
        // sharp ridgelines, turning isolated peaks into realistic mountain chains.
        // The ridge layer is sampled at a coarser scale to produce range-sized features.
        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                int idx = y * width + x;
                double h = adjusted[idx];

                // Only apply ridge sharpening where the terrain is already high,
                // so valleys and plains are unaffected.
                if (h > _settings.RidgeInfluenceThreshold)
                {
                    double ridge = ridgeNoise.Sample(x, y);
                    double blend = (h - _settings.RidgeInfluenceThreshold)
                                 / (1.0 - _settings.RidgeInfluenceThreshold);
                    // Lerp: at threshold ridge has 0 influence; above it, ridge amplifies peaks.
                    h = h + blend * (ridge - 0.5) * _settings.RidgeStrength;
                }

                adjusted[idx] = Math.Clamp(h, 0, 1);
            }
        }

        // ── Step 3: Threshold into Flat / Hilly / Mountainous / Water ─────────
        //
        // We want the waterRatio% of tiles to be water. The hillAndMountainRatio%
        // of LAND tiles should be elevated (split roughly 2:1 hilly:mountainous).
        //
        // Rather than computing exact percentiles (expensive), we pre-sort a sample
        // and derive thresholds that hit the targets.
        // HillAndMountainRatio is a % of LAND tiles, not all tiles — scale the
        // hill/mountain boundaries into the land-only percentile range.
        double w    = _settings.WaterRatio / 100.0;
        double r    = _settings.HillAndMountainRatio / 100.0;
        double land = 1.0 - w;

        double waterLevel = ComputePercentileThreshold(adjusted, _settings.WaterRatio);
        double hillLevel  = ComputePercentileThreshold(adjusted, (w + (1.0 - r) * land) * 100.0);
        double mountLevel = ComputePercentileThreshold(adjusted, (w + (1.0 - r / 3.0) * land) * 100.0);

        bool[]         isWater = new bool[width * height];
        GeographyType[] geo   = new GeographyType[width * height];

        for (int i = 0; i < adjusted.Length; i++)
        {
            double h = adjusted[i];
            if (h < waterLevel)
            {
                // Classified as water; ShallowWater vs DeepWater determined in Step 4.
                geo[i]     = GeographyType.DeepWater;
                isWater[i] = true;
            }
            else if (h < hillLevel)
            {
                geo[i] = GeographyType.Flat;
            }
            else if (h < mountLevel)
            {
                geo[i] = GeographyType.Hilly;
            }
            else
            {
                geo[i] = GeographyType.Mountainous;
            }
        }

        // ── Step 4: Distance transform — mark ShallowWater ───────────────────
        // BFS outward from the water/land boundary. Tiles that are water and
        // within ShallowDepth tiles of land become ShallowWater (Coastal).
        MarkShallowWater(geo, isWater, width, height, _settings.ShallowDepth);

        // ── Step 5: Harbour exception ────────────────────────────────────────
        // Where the local height gradient is very steep (cliff coast), allow a
        // small fraction of adjacent DeepWater/land pairs to remain without a
        // ShallowWater buffer. This represents deep harbours or fjord mouths.
        // We simply re-classify a fraction of ShallowWater tiles that sit between
        // DeepWater and land back to DeepWater.
        ApplyHarbourException(geo, heightmap, width, height, _settings.HarbourChance);

        return geo;
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Helpers
    // ─────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Returns the value at the given percentile in the height array.
    /// E.g. percentile=55 means 55% of tiles are at or below this value.
    /// </summary>
    private static double ComputePercentileThreshold(double[] values, double percentile)
    {
        // Partial sort would be more efficient, but the array is small enough that
        // a full sort is fast and simpler to read.
        double[] sorted = (double[])values.Clone();
        Array.Sort(sorted);
        int idx = (int)(sorted.Length * percentile / 100.0);
        idx = Math.Clamp(idx, 0, sorted.Length - 1);
        return sorted[idx];
    }

    /// <summary>
    /// BFS from each land tile; water tiles within maxDepth steps of land
    /// are reclassified as ShallowWater.
    /// </summary>
    private static void MarkShallowWater(
        GeographyType[] geo,
        bool[]          isWater,
        int             width,
        int             height,
        int             maxDepth)
    {
        // Distance array: 0 = unvisited water, positive = distance from land boundary.
        int[] dist = new int[width * height];
        Array.Fill(dist, int.MaxValue);

        var queue = new Queue<int>();

        // Seed the BFS with all water tiles that are directly adjacent to land.
        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                int i = y * width + x;
                if (!isWater[i]) { continue; }

                if (HasLandNeighbour(isWater, x, y, width, height))
                {
                    dist[i] = 1;
                    queue.Enqueue(i);
                }
            }
        }

        // BFS expansion.
        while (queue.Count > 0)
        {
            int idx = queue.Dequeue();
            int cx  = idx % width;
            int cy  = idx / width;
            int d   = dist[idx];

            if (d >= maxDepth) { continue; }  // don't expand beyond the max depth

            foreach ((int nx, int ny) in FourNeighbours(cx, cy, width, height))
            {
                int ni = ny * width + nx;
                if (isWater[ni] && dist[ni] == int.MaxValue)
                {
                    dist[ni] = d + 1;
                    queue.Enqueue(ni);
                }
            }
        }

        // Reclassify water tiles within maxDepth of land.
        for (int i = 0; i < geo.Length; i++)
        {
            if (isWater[i] && dist[i] <= maxDepth)
            {
                geo[i] = GeographyType.ShallowWater;
            }
        }
    }

    /// <summary>
    /// For steep coastal segments, convert an occasional ShallowWater tile back to DeepWater
    /// so that deep ocean directly abuts the land — simulating cliff-faced harbours.
    /// </summary>
    private static void ApplyHarbourException(
        GeographyType[] geo,
        double[]        originalHeight,
        int             width,
        int             height,
        double          chance)
    {
        if (chance <= 0) { return; }

        // Re-use a fixed RNG (seeded from a constant) so this is deterministic.
        // The actual randomness already comes from the heightmap, so the harbour
        // pattern differs per map without needing the master seed here.
        var rng = new Random(42);

        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                int i = y * width + x;
                if (geo[i] != GeographyType.ShallowWater) { continue; }

                // Measure the local height gradient to detect steep coasts.
                double localGradient = ComputeLocalGradient(originalHeight, x, y, width, height);
                if (localGradient < 0.12) { continue; }   // only steep coasts qualify

                // Check that this shallow tile sits between DeepWater and land.
                bool hasDeep = false;
                bool hasLand = false;
                foreach ((int nx, int ny) in FourNeighbours(x, y, width, height))
                {
                    var ng = geo[ny * width + nx];
                    if (ng == GeographyType.DeepWater)  { hasDeep = true; }
                    if (ng is GeographyType.Flat or GeographyType.Hilly or GeographyType.Mountainous) { hasLand = true; }
                }

                if (hasDeep && hasLand && rng.NextDouble() < chance)
                {
                    geo[i] = GeographyType.DeepWater;
                }
            }
        }
    }

    private static double ComputeLocalGradient(double[] h, int x, int y, int w, int ht)
    {
        // Simple Sobel-like magnitude using neighbours that exist.
        double centre = h[y * w + x];
        double maxDiff = 0;
        foreach ((int nx, int ny) in FourNeighbours(x, y, w, ht))
        {
            double diff = Math.Abs(h[ny * w + nx] - centre);
            if (diff > maxDiff) { maxDiff = diff; }
        }
        return maxDiff;
    }

    private static bool HasLandNeighbour(bool[] isWater, int x, int y, int w, int h)
    {
        foreach ((int nx, int ny) in FourNeighbours(x, y, w, h))
        {
            if (!isWater[ny * w + nx]) { return true; }
        }
        return false;
    }

    private static IEnumerable<(int x, int y)> FourNeighbours(int x, int y, int w, int h)
    {
        if (x > 0)     yield return (x - 1, y);
        if (x < w - 1) yield return (x + 1, y);
        if (y > 0)     yield return (x, y - 1);
        if (y < h - 1) yield return (x, y + 1);
    }
}
