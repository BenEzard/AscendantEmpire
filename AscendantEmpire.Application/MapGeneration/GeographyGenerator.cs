using AscendantEmpire.Application.MapGeneration.Noise;
using AscendantEmpire.Domain.Enums.Maps;

namespace AscendantEmpire.Application.MapGeneration;

/// <summary>
/// Converts a normalised heightmap [0, 1] into a CampaignMapGeographyType per tile.
///
/// Pipeline:
///   1. Apply polar altitude reduction (if Poles=true).
///   2. Blend ridged noise to sharpen mountain ranges.
///   3. Threshold into water / flat / hilly / mountainous.
///   4. BFS distance transform: water tiles near land → ShallowWater.
///   5. Harbour exception: allow occasional DeepWater-to-land contact on steep coasts.
/// </summary>
public sealed class GeographyGenerator
{
    private readonly GeographySettings _s;
    public GeographyGenerator(GeographySettings settings) => _s = settings;

    public CampaignMapGeographyType[] Generate(
        double[]    heightmap,
        int         width,
        int         height,
        RidgedNoise ridgeNoise)
    {
        double[] h = (double[])heightmap.Clone();

        // ── Polar depression ──────────────────────────────────────────────────
        if (_s.Poles)
        {
            for (int y = 0; y < height; y++)
            {
                double distFromPole   = Math.Min(y, height - 1 - y) / (double)(height / 2.0);
                double polarStrength  = Math.Max(0, 1.0 - distFromPole / _s.PolarReach) * _s.PolarDepth;
                for (int x = 0; x < width; x++) { h[y * width + x] -= polarStrength; }
            }
        }

        // ── Ridge blending for mountain ranges ────────────────────────────────
        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                int idx = y * width + x;
                double v = h[idx];
                if (v > _s.RidgeInfluenceThreshold)
                {
                    double ridge = ridgeNoise.Sample(x, y);
                    double blend = (v - _s.RidgeInfluenceThreshold) / (1.0 - _s.RidgeInfluenceThreshold);
                    v = v + blend * (ridge - 0.5) * _s.RidgeStrength;
                }
                h[idx] = Math.Clamp(v, 0, 1);
            }
        }

        // ── Compute thresholds from target percentages ────────────────────────
        // HillAndMountainRatio is expressed as a % of LAND tiles, not all tiles.
        // We scale the hill/mountain boundaries into the land-only portion of the
        // percentile range so water tiles are excluded from the ratio.
        //
        // With w = WaterRatio/100 and land = 1 - w:
        //   hillLevel  → flat/hilly boundary at (1 - r) through the land range
        //                so Flat = (100 - HillAndMountainRatio)% of land tiles
        //   mountLevel → hilly/mountainous boundary at (1 - r/3) through the land range
        //                so Hilly = 2/3 of HillAndMountainRatio% of land tiles,
        //                   Mountainous = 1/3 of HillAndMountainRatio% of land tiles
        double w    = _s.WaterRatio / 100.0;
        double r    = _s.HillAndMountainRatio / 100.0;
        double land = 1.0 - w;

        double waterLevel = Percentile(h, _s.WaterRatio);
        double hillLevel  = Percentile(h, (w + (1.0 - r) * land) * 100.0);
        double mountLevel = Percentile(h, (w + (1.0 - r / 3.0) * land) * 100.0);

        bool[]                    isWater = new bool[width * height];
        CampaignMapGeographyType[] geo    = new CampaignMapGeographyType[width * height];

        for (int i = 0; i < h.Length; i++)
        {
            if (h[i] < waterLevel)       { geo[i] = CampaignMapGeographyType.DeepWater;   isWater[i] = true; }
            else if (h[i] < hillLevel)   { geo[i] = CampaignMapGeographyType.Flat; }
            else if (h[i] < mountLevel)  { geo[i] = CampaignMapGeographyType.Hilly; }
            else                         { geo[i] = CampaignMapGeographyType.Mountainous; }
        }

        // ── ShallowWater BFS ──────────────────────────────────────────────────
        MarkShallowWater(geo, isWater, width, height, _s.ShallowDepth);

        // ── Harbour exception ─────────────────────────────────────────────────
        ApplyHarbourException(geo, heightmap, width, height, _s.HarbourChance);

        return geo;
    }

    private static double Percentile(double[] values, double pct)
    {
        double[] sorted = (double[])values.Clone();
        Array.Sort(sorted);
        int idx = Math.Clamp((int)(sorted.Length * pct / 100.0), 0, sorted.Length - 1);
        return sorted[idx];
    }

    private static void MarkShallowWater(
        CampaignMapGeographyType[] geo,
        bool[]                     isWater,
        int width, int height, int maxDepth)
    {
        int[] dist = new int[width * height];
        Array.Fill(dist, int.MaxValue);
        var queue = new Queue<int>();

        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                int i = y * width + x;
                if (isWater[i] && HasLandNeighbour(isWater, x, y, width, height))
                {
                    dist[i] = 1;
                    queue.Enqueue(i);
                }
            }
        }

        while (queue.Count > 0)
        {
            int idx = queue.Dequeue();
            if (dist[idx] >= maxDepth) { continue; }
            int cx = idx % width;
            int cy = idx / width;
            foreach ((int nx, int ny) in FourNeighbours(cx, cy, width, height))
            {
                int ni = ny * width + nx;
                if (isWater[ni] && dist[ni] == int.MaxValue)
                {
                    dist[ni] = dist[idx] + 1;
                    queue.Enqueue(ni);
                }
            }
        }

        for (int i = 0; i < geo.Length; i++)
        {
            if (isWater[i] && dist[i] <= maxDepth)
            {
                geo[i] = CampaignMapGeographyType.ShallowWater;
            }
        }
    }

    private static void ApplyHarbourException(
        CampaignMapGeographyType[] geo,
        double[] originalHeight,
        int width, int height, double chance)
    {
        if (chance <= 0) { return; }
        var rng = new Random(42);
        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                int i = y * width + x;
                if (geo[i] != CampaignMapGeographyType.ShallowWater) { continue; }
                if (LocalGradient(originalHeight, x, y, width, height) < 0.12) { continue; }

                bool hasDeep = false, hasLand = false;
                foreach ((int nx, int ny) in FourNeighbours(x, y, width, height))
                {
                    var ng = geo[ny * width + nx];
                    if (ng == CampaignMapGeographyType.DeepWater) { hasDeep = true; }
                    if (ng is CampaignMapGeographyType.Flat
                           or CampaignMapGeographyType.Hilly
                           or CampaignMapGeographyType.Mountainous) { hasLand = true; }
                }
                if (hasDeep && hasLand && rng.NextDouble() < chance)
                {
                    geo[i] = CampaignMapGeographyType.DeepWater;
                }
            }
        }
    }

    private static double LocalGradient(double[] h, int x, int y, int w, int ht)
    {
        double centre = h[y * w + x];
        double maxDiff = 0;
        foreach ((int nx, int ny) in FourNeighbours(x, y, w, ht))
        {
            double d = Math.Abs(h[ny * w + nx] - centre);
            if (d > maxDiff) { maxDiff = d; }
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
        if (x > 0)     { yield return (x - 1, y); }
        if (x < w - 1) { yield return (x + 1, y); }
        if (y > 0)     { yield return (x, y - 1); }
        if (y < h - 1) { yield return (x, y + 1); }
    }
}
