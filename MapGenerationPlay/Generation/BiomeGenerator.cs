using MapGenerationPlay.Noise;

namespace MapGenerationPlay.Generation;

/// <summary>
/// Assigns a BiomeType to every campaign-map tile based on geography, latitude-driven
/// temperature, and moisture noise.
///
/// Pipeline:
///   1. Assign water tiles directly (Ocean / Coastal) from geography.
///   2. For land tiles, compute temperature (latitude + noise) and moisture (noise).
///   3. Look up biome from a Whittaker-style table (temperature × moisture → biome).
///   4. Apply a cellular-automaton clustering pass so biomes form natural regions.
///   5. Enforce transition rules via BFS replacement (e.g., no Ice next to Grassland).
///   6. Respect CanBeElevated: biomes that cannot be elevated are reclassified to Flat
///      if they ended up on Hilly or Mountainous geography.
/// </summary>
public sealed class BiomeGenerator
{
    private readonly BiomeSettings _settings;

    public BiomeGenerator(BiomeSettings settings) => _settings = settings;

    /// <summary>
    /// Generates the biome array.
    /// </summary>
    /// <param name="geography">Geography layer produced by GeographyGenerator.</param>
    /// <param name="width">Map width.</param>
    /// <param name="height">Map height.</param>
    /// <param name="tempNoise">
    /// PerlinNoise instance (different seed) used for temperature variation.
    /// </param>
    /// <param name="moistureNoise">
    /// PerlinNoise instance (different seed again) used for moisture.
    /// </param>
    /// <param name="geoOut">
    /// Output geography array — may be modified in-place where CanBeElevated is violated.
    /// </param>
    public BiomeType[] Generate(
        GeographyType[] geography,
        int             width,
        int             height,
        PerlinNoise     tempNoise,
        PerlinNoise     moistureNoise,
        GeographyType[] geoOut)
    {
        BiomeType[] biomes = new BiomeType[width * height];

        // Temperature and moisture fBm (coarser scale for continent-sized climate zones).
        double tempScale     = 2.0 / Math.Max(width, height);
        double moistureScale = 2.5 / Math.Max(width, height);

        // ── Step 1 + 2: water tiles and climate-based biome lookup ────────────
        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                int idx = y * width + x;
                GeographyType geo = geography[idx];

                // Water tiles are determined purely by geography.
                if (geo == GeographyType.ShallowWater)
                {
                    biomes[idx] = BiomeType.Coastal;
                    continue;
                }
                if (geo == GeographyType.DeepWater)
                {
                    biomes[idx] = BiomeType.Ocean;
                    continue;
                }

                // ── Temperature: latitude gradient + noise ───────────────────
                // normalised latitude 0 = top edge (cold), 1 = bottom edge (cold),
                // 0.5 = equator (hot). The V-shape makes both poles cold.
                double latFraction = (double)y / (height - 1);
                // map to [0,1] where 1=equator, 0=poles
                double latHeat = 1.0 - 2.0 * Math.Abs(latFraction - 0.5);

                if (_settings.Equator)
                {
                    // Sharpen the equatorial warm band using a cubic curve.
                    latHeat = Math.Pow(latHeat, 1.5);
                }

                // Sample temperature noise in the 7–8 octave range for fine variation.
                double tempRaw = SampleFbm(tempNoise, x * tempScale, y * tempScale, octaves: 5, persistence: 0.5);
                // Remap from [-1,1] to [0,1].
                double tempNoiseMapped = (tempRaw + 1.0) * 0.5;

                // Blend: mostly latitude-driven with some noise variation.
                double temperature = latHeat * (1.0 - _settings.TemperatureNoiseWeight)
                                   + tempNoiseMapped * _settings.TemperatureNoiseWeight;

                // ── Moisture: pure noise ─────────────────────────────────────
                double moistureRaw = SampleFbm(moistureNoise, x * moistureScale, y * moistureScale, octaves: 5, persistence: 0.5);
                double moisture    = (moistureRaw + 1.0) * 0.5;   // remap to [0,1]

                biomes[idx] = LookupBiome(temperature, moisture);
            }
        }

        // ── Step 3: Cellular-automaton clustering ─────────────────────────────
        // Nudge isolated biome tiles toward the majority biome among their neighbours.
        // This turns salt-and-pepper speckle into smooth, realistic regions.
        for (int pass = 0; pass < _settings.ClusteringPasses; pass++)
        {
            biomes = ClusteringPass(biomes, geography, width, height);
        }

        // ── Step 4: Transition rule enforcement ──────────────────────────────
        // Ensure no biome is directly adjacent to an incompatible biome.
        // E.g. Ice must not border Grassland; Desert must not border Forest.
        EnforceTransitions(biomes, geography, width, height);

        // ── Step 5: CanBeElevated enforcement ────────────────────────────────
        // Reclassify Hilly/Mountainous geography to Flat where the biome cannot be elevated.
        EnforceElevationCompatibility(biomes, geoOut, width, height);

        return biomes;
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Whittaker biome table
    //
    // Temperature and moisture are each split into 4 bands (cold/cool/warm/hot)
    // and (dry/moderate/wet/very-wet). The resulting 4×4 grid maps to BiomeType.
    //
    //                Dry        Moderate   Wet       Very Wet
    //   Cold (0)     Ice        Tundra     Tundra    Tundra
    //   Cool (1)     Tundra     Grassland  Forest    Forest
    //   Warm (2)     Arid       Woodland   Forest    Jungle
    //   Hot  (3)     Desert     Arid       Jungle    Jungle
    //
    // ─────────────────────────────────────────────────────────────────────────

    private static readonly BiomeType[,] WhittakerTable = new BiomeType[4, 4]
    {
        // Dry=0       Moderate=1        Wet=2              Very Wet=3
        { BiomeType.Ice,      BiomeType.Tundra,    BiomeType.Tundra,    BiomeType.Tundra   },  // Cold
        { BiomeType.Tundra,   BiomeType.Grassland, BiomeType.Forest,    BiomeType.Forest   },  // Cool
        { BiomeType.Arid,     BiomeType.Woodland,  BiomeType.Forest,    BiomeType.Jungle   },  // Warm
        { BiomeType.Desert,   BiomeType.Arid,      BiomeType.Jungle,    BiomeType.Jungle   },  // Hot
    };

    private static BiomeType LookupBiome(double temperature, double moisture)
    {
        // Map continuous values to 4 bands each.
        int tempBand     = Math.Clamp((int)(temperature * 4), 0, 3);
        int moistureBand = Math.Clamp((int)(moisture    * 4), 0, 3);
        return WhittakerTable[tempBand, moistureBand];
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Clustering pass
    // ─────────────────────────────────────────────────────────────────────────

    private BiomeType[] ClusteringPass(
        BiomeType[]     biomes,
        GeographyType[] geography,
        int             width,
        int             height)
    {
        BiomeType[] next = (BiomeType[])biomes.Clone();

        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                int idx = y * width + x;

                // Don't cluster water tiles — their biome is fixed by geography.
                if (geography[idx] is GeographyType.ShallowWater or GeographyType.DeepWater) { continue; }

                // Count neighbour biomes (8-connected).
                var counts = new Dictionary<BiomeType, int>();
                int total  = 0;
                foreach ((int nx, int ny) in EightNeighbours(x, y, width, height))
                {
                    int ni = ny * width + nx;
                    if (geography[ni] is GeographyType.ShallowWater or GeographyType.DeepWater) { continue; }

                    var nb = biomes[ni];
                    counts[nb] = counts.GetValueOrDefault(nb, 0) + 1;
                    total++;
                }

                if (total == 0) { continue; }

                // Find the dominant neighbour biome.
                BiomeType dominant      = biomes[idx];
                int       dominantCount = 0;
                foreach (var (biome, cnt) in counts)
                {
                    if (cnt > dominantCount) { dominantCount = cnt; dominant = biome; }
                }

                // Only nudge if the dominant biome passes the threshold fraction.
                if ((double)dominantCount / total >= _settings.ClusteringThreshold)
                {
                    next[idx] = dominant;
                }
            }
        }

        return next;
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Transition enforcement
    //
    // Hard adjacency rules:
    //   Ice       may only border: Tundra, Ocean, Coastal
    //   Tundra    may only border: Ice, Grassland, Forest, Ocean, Coastal
    //   Desert    may only border: Arid, Ocean, Coastal
    //   (All other pairs are permitted)
    //
    // When a violation is found, the tile is BFS-replaced with the nearest
    // compatible biome from its valid neighbours.
    // ─────────────────────────────────────────────────────────────────────────

    private static readonly HashSet<BiomeType> WaterBiomes =
        [BiomeType.Ocean, BiomeType.Coastal];

    private static readonly Dictionary<BiomeType, HashSet<BiomeType>> AllowedNeighbours = new()
    {
        // Ice may only be next to Tundra or water.
        [BiomeType.Ice]    = [BiomeType.Ice, BiomeType.Tundra, BiomeType.Ocean, BiomeType.Coastal],
        // Tundra may not be next to Desert or Jungle.
        [BiomeType.Tundra] = [BiomeType.Tundra, BiomeType.Ice, BiomeType.Grassland, BiomeType.Forest,
                               BiomeType.Woodland, BiomeType.Ocean, BiomeType.Coastal],
        // Desert may only border Arid or water (not Grassland, Forest etc. directly).
        [BiomeType.Desert] = [BiomeType.Desert, BiomeType.Arid, BiomeType.Ocean, BiomeType.Coastal],
    };

    private static void EnforceTransitions(
        BiomeType[]     biomes,
        GeographyType[] geography,
        int             width,
        int             height)
    {
        // Iterate until no violations remain (usually 1–2 passes suffice).
        bool anyFixed = true;
        int  safetyLimit = 10;

        while (anyFixed && safetyLimit-- > 0)
        {
            anyFixed = false;

            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    int idx = y * width + x;
                    if (!AllowedNeighbours.TryGetValue(biomes[idx], out var allowed)) { continue; }

                    foreach ((int nx, int ny) in FourNeighbours(x, y, width, height))
                    {
                        int ni = ny * width + nx;
                        if (!allowed.Contains(biomes[ni]))
                        {
                            // The neighbour biome is invalid next to biomes[idx].
                            // Replace the neighbour via BFS from its valid land neighbours.
                            biomes[ni] = FindNearestCompatibleBiome(biomes, geography, ni, biomes[idx], width, height);
                            anyFixed   = true;
                        }
                    }
                }
            }
        }
    }

    /// <summary>
    /// BFS from <paramref name="startIdx"/>, looking for the nearest land tile whose
    /// biome is compatible with <paramref name="requiredCompatibleWith"/>.
    /// Returns that biome, or Grassland as a safe fallback.
    /// </summary>
    private static BiomeType FindNearestCompatibleBiome(
        BiomeType[]     biomes,
        GeographyType[] geography,
        int             startIdx,
        BiomeType       requiredCompatibleWith,
        int             width,
        int             height)
    {
        // Determine which biomes are allowed next to requiredCompatibleWith.
        HashSet<BiomeType>? allowed = null;
        AllowedNeighbours.TryGetValue(requiredCompatibleWith, out allowed);

        var visited = new HashSet<int> { startIdx };
        var queue   = new Queue<int>();
        queue.Enqueue(startIdx);

        while (queue.Count > 0)
        {
            int idx = queue.Dequeue();
            int cx  = idx % width;
            int cy  = idx / width;

            foreach ((int nx, int ny) in FourNeighbours(cx, cy, width, height))
            {
                int ni = ny * width + nx;
                if (visited.Contains(ni)) { continue; }
                visited.Add(ni);

                if (geography[ni] is GeographyType.ShallowWater or GeographyType.DeepWater)
                {
                    queue.Enqueue(ni);
                    continue;
                }

                BiomeType candidate = biomes[ni];

                // If no restriction on requiredCompatibleWith, any land biome is fine.
                if (allowed is null || allowed.Contains(candidate))
                {
                    return candidate;
                }

                queue.Enqueue(ni);
            }
        }

        return BiomeType.Grassland; // safe fallback; extremely unlikely to be reached
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Elevation compatibility
    // ─────────────────────────────────────────────────────────────────────────

    private void EnforceElevationCompatibility(
        BiomeType[]     biomes,
        GeographyType[] geography,
        int             width,
        int             height)
    {
        for (int i = 0; i < biomes.Length; i++)
        {
            if (geography[i] is GeographyType.Hilly or GeographyType.Mountainous
                && _settings.CannotBeElevated.Contains(biomes[i]))
            {
                // Demote the geography to Flat so the domain model constraints are respected.
                geography[i] = GeographyType.Flat;
            }
        }
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Helpers
    // ─────────────────────────────────────────────────────────────────────────

    /// <summary>Basic multi-octave fBm without the full class, for inline use.</summary>
    private static double SampleFbm(PerlinNoise noise, double x, double y, int octaves, double persistence)
    {
        double value     = 0;
        double amplitude = 1;
        double frequency = 1;
        double maxValue  = 0;

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
                int nx = x + dx;
                int ny = y + dy;
                if (nx >= 0 && nx < w && ny >= 0 && ny < h) { yield return (nx, ny); }
            }
        }
    }
}
