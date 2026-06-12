using MapGenerationPlay.Noise;

namespace MapGenerationPlay.Generation;

/// <summary>
/// Generates a single 50×50 BattleMap for a given campaign tile (camX, camY).
///
/// Key properties:
///
///   Edge continuity:
///     The BattleMap noise is sampled from the *same underlying Perlin field* as the
///     Campaign Map, just zoomed in. Tile (camX, camY) samples the noise domain at
///     x ∈ [camX * size, (camX+1) * size) and y ∈ [camY * size, (camY+1) * size).
///     Because the Perlin field is globally continuous, adjacent BattleMaps share the
///     same noise values at their common edge — seams are invisible.
///
///   Derived seed:
///     localSeed = masterSeed XOR (camX * prime1) XOR (camY * prime2)
///     The primes avoid trivial collisions (e.g. seed=X,camX=Y producing the same result
///     as seed=Y,camX=X).  This seed is used only for the biome-diversity noise pass, NOT
///     for the altitude heightmap (which uses the shared field above).
///
///   Geography influence:
///     The neighbouring campaign tiles' geography types bleed into the edges of this
///     BattleMap.  If the left neighbour is Mountainous, the leftmost columns have their
///     altitude raised by a gradient.  This ensures the visual transition between a hilly
///     tile and an adjacent mountain is felt within the battle itself.
///
///   Altitude thresholds:
///     Controlled by the campaign tile's own geography type (Flat, Hilly).
///     Flat → mostly Ground with occasional Low.
///     Hilly → mostly Low/Medium with occasional High.
/// </summary>
public sealed class BattleMapGenerator
{
    // Large primes used in the derived seed hash.
    private const long _primeX = 2654435761L;
    private const long _primeY = 2246822519L;

    /// <summary>
    /// Generates a BattleMap for campaign tile (camX, camY).
    /// </summary>
    /// <param name="masterSeed">The campaign map's master seed.</param>
    /// <param name="camX">X coordinate of the campaign tile.</param>
    /// <param name="camY">Y coordinate of the campaign tile.</param>
    /// <param name="battleSize">Width = Height of the battle map (square).</param>
    /// <param name="tileTotalWidth">Total number of campaign tiles wide (for noise scaling).</param>
    /// <param name="tileTotalHeight">Total number of campaign tiles tall (for noise scaling).</param>
    /// <param name="tileGeo">Geography of the campaign tile being zoomed in on.</param>
    /// <param name="neighbourGeo">
    /// Geography of the four orthogonal neighbours, in order: Left, Right, Up, Down.
    /// Pass null for any neighbour that doesn't exist (edge of the campaign map).
    /// </param>
    public BattleMapResult Generate(
        long          masterSeed,
        long          gameSeed,
        int           camX,
        int           camY,
        int           battleSize,
        int           tileTotalWidth,
        int           tileTotalHeight,
        GeographyType tileGeo,
        GeographyType?[] neighbourGeo)   // [Left, Right, Up, Down]
    {
        // ── Build the shared noise field ──────────────────────────────────────
        // Mix gameSeed into masterSeed so each game produces unique altitude topology
        // while adjacent tiles within the same game remain continuous at their edges.
        long combinedSeed = masterSeed ^ gameSeed;
        var terrainNoise = new PerlinNoise(combinedSeed);
        var warpNoise    = new PerlinNoise(combinedSeed ^ unchecked((long)0x9E3779B97F4A7C15UL));

        var terrainFbm = new FractalBrownianMotion(terrainNoise)
        {
            Octaves     = 8,    // more octaves at fine scale for richer detail
            Persistence = 0.5,
            Lacunarity  = 2.0,
            // Campaign-level scale gives continent-sized features. BattleMap zooms into
            // one tile, so we divide by the full campaign size in noise space.
            Scale       = 3.0 / Math.Max(tileTotalWidth, tileTotalHeight),
        };

        var domainWarped = new DomainWarpedNoise(terrainFbm, warpNoise, doubleWarp: true)
        {
            WarpStrength = 0.15,
        };

        // ── Sample the heightmap for this tile's area ─────────────────────────
        // Each campaign tile occupies one unit of the noise domain in campaign-tile space.
        // We subdivide that unit into battleSize steps to sample the fine detail.
        double[] heightmap = new double[battleSize * battleSize];
        double min = double.MaxValue, max = double.MinValue;

        for (int by = 0; by < battleSize; by++)
        {
            // Map battle y [0, battleSize) to campaign noise x in [camY, camY+1).
            double noiseY = camY + (by + 0.5) / battleSize;

            for (int bx = 0; bx < battleSize; bx++)
            {
                double noiseX = camX + (bx + 0.5) / battleSize;
                double h = domainWarped.Sample(noiseX, noiseY);
                heightmap[by * battleSize + bx] = h;
                if (h < min) { min = h; }
                if (h > max) { max = h; }
            }
        }

        // Normalise to [0, 1] within this tile.
        double range = max - min;
        if (range < 1e-10) { range = 1; }
        for (int i = 0; i < heightmap.Length; i++) { heightmap[i] = (heightmap[i] - min) / range; }

        // ── Apply neighbour geography edge blending ───────────────────────────
        // Mountainous neighbours push altitude up at the shared edge; Flat neighbours
        // pull it down.  The blend falls off over the first ~20% of the tile width/height.
        ApplyNeighbourBlend(heightmap, battleSize, neighbourGeo);

        // ── Classify altitudes from the blended heightmap ─────────────────────
        BattleMapAltitude[] altitudes = ClassifyAltitude(heightmap, battleSize, tileGeo);

        // ── Biome diversity pass ──────────────────────────────────────────────
        // Use a fine-scale noise pass with a derived seed to add small patches of
        // neighbouring-compatible biomes within the tile. The BiomeType returned here is
        // just for visualisation in the prototype; the Application layer uses the
        // actual Domain BiomeType when inserting into the database.
        long localSeed = gameSeed ^ (camX * _primeX) ^ (camY * _primeY);
        BiomeType[] biomes = AssignBiomes(localSeed, heightmap, altitudes, battleSize);

        return new BattleMapResult(altitudes, biomes, localSeed);
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Edge blending
    // ─────────────────────────────────────────────────────────────────────────

    private static readonly Dictionary<GeographyType, double> _geoElevationBias = new()
    {
        [GeographyType.DeepWater]    = -0.30,
        [GeographyType.ShallowWater] = -0.15,
        [GeographyType.Flat]         =  0.00,
        [GeographyType.Hilly]        =  0.20,
        [GeographyType.Mountainous]  =  0.45,
    };

    private static void ApplyNeighbourBlend(double[] h, int size, GeographyType?[] neighbours)
    {
        // neighbours = [Left=0, Right=1, Up=2, Down=3]
        double blendWidth = size * 0.20;  // the blend zone is 20% of the tile

        for (int i = 0; i < 4; i++)
        {
            if (neighbours[i] is null) { continue; }
            double bias = _geoElevationBias.GetValueOrDefault(neighbours[i]!.Value, 0);
            if (Math.Abs(bias) < 0.01) { continue; }  // no meaningful effect

            for (int by = 0; by < size; by++)
            {
                for (int bx = 0; bx < size; bx++)
                {
                    // Distance from the relevant edge, in tiles.
                    double distFromEdge = i switch
                    {
                        0 => bx,           // Left: distance from x=0
                        1 => size - 1 - bx, // Right: distance from x=size-1
                        2 => by,           // Up: distance from y=0
                        3 => size - 1 - by, // Down: distance from y=size-1
                        _ => size
                    };

                    if (distFromEdge >= blendWidth) { continue; }

                    // Linear falloff: full bias at the edge, zero at blendWidth.
                    double weight = 1.0 - distFromEdge / blendWidth;
                    int idx = by * size + bx;
                    h[idx] = Math.Clamp(h[idx] + bias * weight, 0, 1);
                }
            }
        }
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Altitude classification
    // ─────────────────────────────────────────────────────────────────────────

    // Thresholds: each row represents how the height [0,1] maps to altitude bands
    // for a given campaign geography type. BelowWater is only assigned if the tile
    // itself was ShallowWater/DeepWater (handled outside classification).
    private static readonly Dictionary<GeographyType, double[]> _altitudeThresholds = new()
    {
        // Flat: predominantly Ground; rare higher ground.
        [GeographyType.Flat] = [0.55, 0.85, 0.95, 0.99],
        // Hilly: low + medium dominate; some high; rare peak.
        [GeographyType.Hilly] = [0.20, 0.55, 0.80, 0.95],
        // Water tiles: mostly below-water with a little ground (islands, sandbars).
        [GeographyType.ShallowWater] = [0.70, 0.90, 0.97, 0.99],
        [GeographyType.DeepWater]    = [0.90, 0.97, 0.99, 1.00],
    };

    private static BattleMapAltitude[] ClassifyAltitude(
        double[]      h,
        int           size,
        GeographyType tileGeo)
    {
        double[] thresholds = _altitudeThresholds.GetValueOrDefault(
            tileGeo,
            _altitudeThresholds[GeographyType.Flat]);  // safe default

        BattleMapAltitude[] alts = new BattleMapAltitude[size * size];
        for (int i = 0; i < h.Length; i++)
        {
            alts[i] = h[i] < thresholds[0] ? BattleMapAltitude.Ground  :
                      h[i] < thresholds[1] ? BattleMapAltitude.Low      :
                      h[i] < thresholds[2] ? BattleMapAltitude.Medium   :
                      h[i] < thresholds[3] ? BattleMapAltitude.High     :
                                             BattleMapAltitude.Peak;

            // Water geography tiles: below-water replaces Ground.
            if (tileGeo is GeographyType.ShallowWater or GeographyType.DeepWater
                && alts[i] == BattleMapAltitude.Ground)
            {
                alts[i] = BattleMapAltitude.BelowWater;
            }
        }
        return alts;
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Biome diversity
    // ─────────────────────────────────────────────────────────────────────────

    private static BiomeType[] AssignBiomes(
        long            localSeed,
        double[]        h,
        BattleMapAltitude[] alts,
        int             size)
    {
        // Fine-scale moisture variation noise for within-tile biome diversity.
        var diversityNoise = new PerlinNoise(localSeed ^ unchecked((long)0xC4CEB9FE1A85EC53UL));
        double diversityScale = 5.0 / size;

        // We don't know the campaign biome in the prototype's BattleMapGenerator,
        // so just derive it from altitude as a stand-in. The Application's version
        // will receive the actual campaign biome as a parameter.
        BiomeType[] biomes = new BiomeType[size * size];
        for (int by = 0; by < size; by++)
        {
            for (int bx = 0; bx < size; bx++)
            {
                int idx = by * size + bx;
                BattleMapAltitude alt = alts[idx];

                // Water tiles get a water biome.
                if (alt == BattleMapAltitude.BelowWater) { biomes[idx] = BiomeType.Coastal; continue; }

                // Use diversity noise to add local variation.
                double diversity = (diversityNoise.Sample(bx * diversityScale, by * diversityScale) + 1.0) * 0.5;
                biomes[idx] = diversity switch
                {
                    < 0.25 => BiomeType.Grassland,
                    < 0.55 => BiomeType.Woodland,
                    < 0.80 => BiomeType.Forest,
                    _      => BiomeType.Arid,
                };
            }
        }
        return biomes;
    }
}
