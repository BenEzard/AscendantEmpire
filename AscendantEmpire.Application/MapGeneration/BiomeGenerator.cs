using AscendantEmpire.Application.MapGeneration.Noise;
using AscendantEmpire.Domain.Enums.Maps;

namespace AscendantEmpire.Application.MapGeneration;

/// <summary>
/// Assigns a BiomeType to every campaign-map tile using geography, latitude-driven temperature,
/// and moisture noise.
///
/// Pipeline:
///   1. Water tiles → Coastal (ShallowWater) or Ocean (DeepWater) from geography.
///   2. Land tiles → Whittaker biome lookup from temperature × moisture.
///   3. Cellular-automaton clustering to produce natural-looking biome regions.
///   4. BFS transition enforcement (e.g. no Ice next to Desert).
///   5. CanBeElevated enforcement: reclassify geography to Flat where the biome disallows elevation.
/// </summary>
public sealed class BiomeGenerator
{
    private readonly BiomeSettings _s;
    public BiomeGenerator(BiomeSettings settings) => _s = settings;

    public BiomeType[] Generate(
        CampaignMapGeographyType[] geography,
        int                         width,
        int                         height,
        PerlinNoise                 tempNoise,
        PerlinNoise                 moistureNoise,
        CampaignMapGeographyType[]  geoOut)
    {
        BiomeType[] biomes = new BiomeType[width * height];
        double tempScale     = 2.0 / Math.Max(width, height);
        double moistureScale = 2.5 / Math.Max(width, height);

        // ── Step 1 + 2: assign biomes ─────────────────────────────────────────
        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                int idx = y * width + x;
                var geo = geography[idx];

                if (geo == CampaignMapGeographyType.ShallowWater) { biomes[idx] = BiomeType.Coastal; continue; }
                if (geo == CampaignMapGeographyType.DeepWater)    { biomes[idx] = BiomeType.Ocean;   continue; }

                // Temperature: V-shape latitude (both poles cold, equator hot) + noise variation.
                double latFraction = (double)y / (height - 1);
                double latHeat     = 1.0 - 2.0 * Math.Abs(latFraction - 0.5);
                if (_s.Equator) { latHeat = Math.Pow(latHeat, 1.5); }

                double tempRaw    = SampleFbm(tempNoise,     x * tempScale,     y * tempScale,     5, 0.5);
                double moistRaw   = SampleFbm(moistureNoise, x * moistureScale, y * moistureScale, 5, 0.5);
                double temperature = latHeat * (1.0 - _s.TemperatureNoiseWeight)
                                   + (tempRaw + 1.0) * 0.5 * _s.TemperatureNoiseWeight;
                double moisture    = (moistRaw + 1.0) * 0.5;

                biomes[idx] = LookupBiome(temperature, moisture);
            }
        }

        // ── Step 3: clustering ────────────────────────────────────────────────
        for (int pass = 0; pass < _s.ClusteringPasses; pass++)
        {
            biomes = ClusteringPass(biomes, geography, width, height);
        }

        // ── Step 4: transition enforcement ───────────────────────────────────
        EnforceTransitions(biomes, geography, width, height);

        // ── Step 5: elevation compatibility ──────────────────────────────────
        EnforceElevationCompatibility(biomes, geoOut, width, height);

        return biomes;
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Whittaker table  (tempBand × moistureBand → biome)
    //
    //            Dry=0       Moderate=1    Wet=2         VeryWet=3
    //  Cold=0    Ice         Tundra        Tundra        Tundra
    //  Cool=1    Tundra      Grassland     Forest        Forest
    //  Warm=2    Arid        Woodland      Forest        Jungle
    //  Hot=3     Desert      Arid          Jungle        Jungle
    // ─────────────────────────────────────────────────────────────────────────

    private static readonly BiomeType[,] _whittaker = new BiomeType[4, 4]
    {
        { BiomeType.Ice,     BiomeType.Tundra,    BiomeType.Tundra,  BiomeType.Tundra  },
        { BiomeType.Tundra,  BiomeType.Grassland, BiomeType.Forest,  BiomeType.Forest  },
        { BiomeType.Arid,    BiomeType.Woodland,  BiomeType.Forest,  BiomeType.Jungle  },
        { BiomeType.Desert,  BiomeType.Arid,      BiomeType.Jungle,  BiomeType.Jungle  },
    };

    private static BiomeType LookupBiome(double temperature, double moisture)
    {
        int t = Math.Clamp((int)(temperature * 4), 0, 3);
        int m = Math.Clamp((int)(moisture    * 4), 0, 3);
        return _whittaker[t, m];
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Clustering
    // ─────────────────────────────────────────────────────────────────────────

    private BiomeType[] ClusteringPass(BiomeType[] biomes, CampaignMapGeographyType[] geo, int w, int h)
    {
        BiomeType[] next = (BiomeType[])biomes.Clone();
        for (int y = 0; y < h; y++)
        {
            for (int x = 0; x < w; x++)
            {
                int idx = y * w + x;
                if (geo[idx] is CampaignMapGeographyType.ShallowWater
                              or CampaignMapGeographyType.DeepWater) { continue; }

                var counts = new Dictionary<BiomeType, int>();
                int total  = 0;
                foreach ((int nx, int ny) in EightNeighbours(x, y, w, h))
                {
                    int ni = ny * w + nx;
                    if (geo[ni] is CampaignMapGeographyType.ShallowWater
                                or CampaignMapGeographyType.DeepWater) { continue; }
                    counts[biomes[ni]] = counts.GetValueOrDefault(biomes[ni], 0) + 1;
                    total++;
                }

                if (total == 0) { continue; }
                BiomeType dom = biomes[idx]; int domCount = 0;
                foreach (var (b, c) in counts) { if (c > domCount) { domCount = c; dom = b; } }
                if ((double)domCount / total >= _s.ClusteringThreshold) { next[idx] = dom; }
            }
        }
        return next;
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Transition enforcement
    // ─────────────────────────────────────────────────────────────────────────

    private static readonly Dictionary<BiomeType, HashSet<BiomeType>> _allowed = new()
    {
        [BiomeType.Ice]    = [BiomeType.Ice, BiomeType.Tundra, BiomeType.Ocean, BiomeType.Coastal],
        [BiomeType.Tundra] = [BiomeType.Tundra, BiomeType.Ice, BiomeType.Grassland, BiomeType.Forest,
                               BiomeType.Woodland, BiomeType.Ocean, BiomeType.Coastal],
        [BiomeType.Desert] = [BiomeType.Desert, BiomeType.Arid, BiomeType.Ocean, BiomeType.Coastal],
    };

    private static void EnforceTransitions(BiomeType[] biomes, CampaignMapGeographyType[] geo, int w, int h)
    {
        bool anyFixed = true;
        int limit = 10;
        while (anyFixed && limit-- > 0)
        {
            anyFixed = false;
            for (int y = 0; y < h; y++)
            {
                for (int x = 0; x < w; x++)
                {
                    int idx = y * w + x;
                    if (!_allowed.TryGetValue(biomes[idx], out var allowed)) { continue; }
                    foreach ((int nx, int ny) in FourNeighbours(x, y, w, h))
                    {
                        int ni = ny * w + nx;
                        if (!allowed.Contains(biomes[ni]))
                        {
                            biomes[ni] = FindNearest(biomes, geo, ni, biomes[idx], w, h);
                            anyFixed   = true;
                        }
                    }
                }
            }
        }
    }

    private static BiomeType FindNearest(
        BiomeType[] biomes, CampaignMapGeographyType[] geo,
        int startIdx, BiomeType compatibleWith, int w, int h)
    {
        _allowed.TryGetValue(compatibleWith, out var allowed);
        var visited = new HashSet<int> { startIdx };
        var queue   = new Queue<int>();
        queue.Enqueue(startIdx);

        while (queue.Count > 0)
        {
            int idx = queue.Dequeue();
            int cx  = idx % w, cy = idx / w;
            foreach ((int nx, int ny) in FourNeighbours(cx, cy, w, h))
            {
                int ni = ny * w + nx;
                if (!visited.Add(ni)) { continue; }
                if (geo[ni] is CampaignMapGeographyType.ShallowWater
                            or CampaignMapGeographyType.DeepWater) { queue.Enqueue(ni); continue; }
                if (allowed is null || allowed.Contains(biomes[ni])) { return biomes[ni]; }
                queue.Enqueue(ni);
            }
        }
        return BiomeType.Grassland;
    }

    private void EnforceElevationCompatibility(
        BiomeType[] biomes, CampaignMapGeographyType[] geoOut, int w, int h)
    {
        for (int i = 0; i < biomes.Length; i++)
        {
            if (geoOut[i] is CampaignMapGeographyType.Hilly or CampaignMapGeographyType.Mountainous
                && _s.CannotBeElevated.Contains(biomes[i]))
            {
                geoOut[i] = CampaignMapGeographyType.Flat;
            }
        }
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Helpers
    // ─────────────────────────────────────────────────────────────────────────

    private static double SampleFbm(PerlinNoise noise, double x, double y, int octaves, double persistence)
    {
        double value = 0, amplitude = 1, frequency = 1, maxValue = 0;
        for (int i = 0; i < octaves; i++)
        {
            value    += noise.Sample(x * frequency, y * frequency) * amplitude;
            maxValue += amplitude;
            amplitude *= persistence;
            frequency *= 2.0;
        }
        return value / maxValue;
    }

    private static IEnumerable<(int x, int y)> FourNeighbours(int x, int y, int w, int h)
    {
        if (x > 0)     { yield return (x - 1, y); }
        if (x < w - 1) { yield return (x + 1, y); }
        if (y > 0)     { yield return (x, y - 1); }
        if (y < h - 1) { yield return (x, y + 1); }
    }

    private static IEnumerable<(int x, int y)> EightNeighbours(int x, int y, int w, int h)
    {
        for (int dy = -1; dy <= 1; dy++)
        {
            for (int dx = -1; dx <= 1; dx++)
            {
                if (dx == 0 && dy == 0) { continue; }
                int nx = x + dx, ny = y + dy;
                if (nx >= 0 && nx < w && ny >= 0 && ny < h) { yield return (nx, ny); }
            }
        }
    }
}
