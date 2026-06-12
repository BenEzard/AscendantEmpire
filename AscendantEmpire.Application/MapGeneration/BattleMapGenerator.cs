using AscendantEmpire.Application.MapGeneration.Noise;
using AscendantEmpire.Domain.Enums.Maps;

namespace AscendantEmpire.Application.MapGeneration;

/// <summary>
/// Generates a single BattleMap (50×50 or configured size) for a given campaign tile.
///
/// Edge continuity guarantee:
///   The heightmap is sampled from the same underlying Perlin noise field used by the Campaign Map, zoomed into the tile's sub-region.  
///   Tile (camX, camY) samples the noise domain at x ∈ [camX, camX+1) and y ∈ [camY, camY+1). 
///   Because the Perlin field is globally continuous, adjacent BattleMaps share identical noise values at their common edge — the seam is invisible.
///
/// Altitude thresholds scale with the campaign tile's geography type:
///   Flat  → mostly Ground with occasional Low.
///   Hilly → Low/Medium dominant with occasional High.
///   Water → mostly BelowWater; islands/sandbars emerge at the high end.
///
/// Neighbour geography blending:
///   The four orthogonal campaign neighbours contribute a gradient to the edge of this BattleMap.  
///   A Mountainous neighbour raises altitude near that edge; a Flat or water neighbour lowers it.  This creates the impression of terrain flowing continuously
///   across campaign-tile boundaries during battles.
///
/// Biome diversity:
///   A fine-scale noise pass (derived from the local seed) adds small patches of nearby compatible biomes within the tile. 
///   E.g., a Grassland tile may contain Woodland and Arid patches.  The campaign biome serves as the "majority" type.
/// </summary>
public sealed class BattleMapGenerator
{
    // Well-distributed primes for the derived-seed hash.
    private const long _primeX = 2654435761L;
    private const long _primeY = 2246822519L;

    /// <summary>
    /// Result of generating one BattleMap tile.
    /// </summary>
    public record BattleMapData(
        BattleMapAltitude[] Altitudes,
        BiomeType[]         Biomes,
        long                LocalSeed
    );

    /// <summary>
    /// Generates the altitude and biome arrays for one BattleMap tile.
    /// </summary>
    /// <param name="masterSeed">
    /// The campaign map's master seed (from CampaignMapGalleryIndex.PropertiesJson).
    /// Used for terrain altitude only — ensures edge continuity across tiles.
    /// </param>
    /// <param name="gameSeed">
    /// The game's generation seed (from GameIndex.GenerationSeed).
    /// Used for biome diversity — gives each game a unique biome distribution
    /// even when two games share the same campaign map.
    /// </param>
    /// <param name="camX">X coordinate of this campaign tile.</param>
    /// <param name="camY">Y coordinate of this campaign tile.</param>
    /// <param name="battleSize">Width = Height of the battle map.</param>
    /// <param name="campaignWidth">Total campaign map width (tiles).</param>
    /// <param name="campaignHeight">Total campaign map height (tiles).</param>
    /// <param name="tileGeo">Geography of the campaign tile being zoomed into.</param>
    /// <param name="tileBiome">Majority biome of the campaign tile.</param>
    /// <param name="neighbourGeo">
    /// Orthogonal neighbours in order [Left, Right, Up, Down].
    /// Null entries indicate a missing neighbour (edge of the campaign map).
    /// </param>
    public BattleMapData Generate(
        long masterSeed,
        long gameSeed,
        int camX,
        int camY,
        int battleSize,
        int campaignWidth,
        int campaignHeight,
        CampaignMapGeographyType tileGeo,
        BiomeType tileBiome,
        CampaignMapGeographyType?[] neighbourGeo)
    {
        // ── Shared noise field ────────────────────────────────────────────────
        // Mix gameSeed into masterSeed so each game produces unique altitude topology.
        // Adjacent tiles within the same game still share this combined seed, preserving edge continuity.
        long combinedSeed = masterSeed ^ gameSeed;
        var terrainNoise = new PerlinNoise(combinedSeed);
        var warpNoise = new PerlinNoise(combinedSeed ^ unchecked((long)0x9E3779B97F4A7C15UL));

        var terrainFbm = new FractalBrownianMotion(terrainNoise)
        {
            Octaves = 8,
            Persistence = 0.5,
            Lacunarity = 2.0,
            Scale = 3.0 / Math.Max(campaignWidth, campaignHeight),
        };

        var domainWarped = new DomainWarpedNoise(terrainFbm, warpNoise, doubleWarp: true)
        {
            WarpStrength = 0.15,
        };

        // ── Sample heightmap for this tile's sub-region ───────────────────────
        double[] heightmap = new double[battleSize * battleSize];
        double min = double.MaxValue, max = double.MinValue;

        for (int by = 0; by < battleSize; by++)
        {
            double noiseY = camY + (by + 0.5) / battleSize;
            for (int bx = 0; bx < battleSize; bx++)
            {
                double noiseX = camX + (bx + 0.5) / battleSize;
                double h = domainWarped.Sample(noiseX, noiseY);
                heightmap[by * battleSize + bx] = h;
                if (h < min) 
                { 
                    min = h; 
                }
                if (h > max) 
                { 
                    max = h; 
                }
            }
        }

        double range = max - min;
        if (range < 1e-10) 
        { 
            range = 1; 
        }
        for (int i = 0; i < heightmap.Length; i++) 
        { 
            heightmap[i] = (heightmap[i] - min) / range; 
        }

        // ── Neighbour edge blending ───────────────────────────────────────────
        ApplyNeighbourBlend(heightmap, battleSize, neighbourGeo);

        // ── Altitude classification ───────────────────────────────────────────
        BattleMapAltitude[] altitudes = ClassifyAltitude(heightmap, battleSize, tileGeo);

        // ── Biome diversity ───────────────────────────────────────────────────
        // Use the game seed (not the campaign seed) so two games on the same campaign
        // map produce different biome distributions while keeping identical altitude.
        long localSeed = gameSeed ^ (camX * _primeX) ^ (camY * _primeY);
        BiomeType[] biomes = AssignBiomes(localSeed, altitudes, battleSize, tileBiome);

        return new BattleMapData(altitudes, biomes, localSeed);
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Edge blending
    // ─────────────────────────────────────────────────────────────────────────

    private static readonly Dictionary<CampaignMapGeographyType, double> _geoBias = new()
    {
        [CampaignMapGeographyType.DeepWater] = -0.30,
        [CampaignMapGeographyType.ShallowWater] = -0.15,
        [CampaignMapGeographyType.Flat] = 0.00,
        [CampaignMapGeographyType.Hilly] = 0.20,
        [CampaignMapGeographyType.Mountainous] = 0.45,
    };

    private static void ApplyNeighbourBlend(
        double[] h, int size, CampaignMapGeographyType?[] neighbours)
    {
        double blendWidth = size * 0.20;

        for (int side = 0; side < 4; side++)
        {
            if (neighbours[side] is null) { continue; }
            double bias = _geoBias.GetValueOrDefault(neighbours[side]!.Value, 0);
            if (Math.Abs(bias) < 0.01) { continue; }

            for (int by = 0; by < size; by++)
            {
                for (int bx = 0; bx < size; bx++)
                {
                    double dist = side switch
                    {
                        0 => bx,
                        1 => size - 1 - bx,
                        2 => by,
                        3 => size - 1 - by,
                        _ => size
                    };
                    if (dist >= blendWidth) { continue; }
                    double weight = 1.0 - dist / blendWidth;
                    int idx = by * size + bx;
                    h[idx] = Math.Clamp(h[idx] + bias * weight, 0, 1);
                }
            }
        }
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Altitude classification
    // ─────────────────────────────────────────────────────────────────────────

    // [Ground, Low, Medium, High] thresholds per campaign geography type.
    // Values above the last threshold become Peak.
    private static readonly Dictionary<CampaignMapGeographyType, double[]> _thresholds = new()
    {
        [CampaignMapGeographyType.Flat] = [0.55, 0.85, 0.95, 0.99],
        [CampaignMapGeographyType.Hilly] = [0.20, 0.55, 0.80, 0.95],
        [CampaignMapGeographyType.ShallowWater] = [0.70, 0.90, 0.97, 0.99],
        [CampaignMapGeographyType.DeepWater] = [0.90, 0.97, 0.99, 1.00],
    };

    private static BattleMapAltitude[] ClassifyAltitude(
        double[] h, int size, CampaignMapGeographyType tileGeo)
    {
        double[] t = _thresholds.GetValueOrDefault(tileGeo, _thresholds[CampaignMapGeographyType.Flat]);
        var alts = new BattleMapAltitude[size * size];

        for (int i = 0; i < h.Length; i++)
        {
            alts[i] = h[i] < t[0] ? BattleMapAltitude.Ground :
                      h[i] < t[1] ? BattleMapAltitude.Low :
                      h[i] < t[2] ? BattleMapAltitude.Medium :
                      h[i] < t[3] ? BattleMapAltitude.High :
                                    BattleMapAltitude.Peak;

            if (tileGeo is CampaignMapGeographyType.ShallowWater
                        or CampaignMapGeographyType.DeepWater
                && alts[i] == BattleMapAltitude.Ground)
            {
                alts[i] = BattleMapAltitude.BelowWater;
            }
        }
        return alts;
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Biome diversity
    //
    // The campaign biome is the "base." A fine-scale noise pass introduces small
    // patches of nearby compatible biomes for visual variety within the battle.
    // Water tiles always get a water biome.
    // ─────────────────────────────────────────────────────────────────────────

    private static readonly Dictionary<BiomeType, BiomeType[]> _compatibleVariants = new()
    {
        [BiomeType.Grassland] = [BiomeType.Grassland, BiomeType.Woodland, BiomeType.Arid],
        [BiomeType.Forest] = [BiomeType.Forest, BiomeType.Woodland, BiomeType.Grassland],
        [BiomeType.Woodland] = [BiomeType.Woodland, BiomeType.Forest, BiomeType.Grassland],
        [BiomeType.Jungle] = [BiomeType.Jungle, BiomeType.Forest,  BiomeType.Woodland],
        [BiomeType.Arid] = [BiomeType.Arid, BiomeType.Grassland, BiomeType.Desert],
        [BiomeType.Desert] = [BiomeType.Desert, BiomeType.Arid],
        [BiomeType.Tundra] = [BiomeType.Tundra, BiomeType.Grassland],
        [BiomeType.Ice] = [BiomeType.Ice, BiomeType.Tundra],
        [BiomeType.Coastal] = [BiomeType.Coastal],
        [BiomeType.Ocean] = [BiomeType.Ocean],
    };

    private static BiomeType[] AssignBiomes(
        long localSeed, BattleMapAltitude[] alts, int size, BiomeType campaignBiome)
    {
        var diversityNoise = new PerlinNoise(localSeed ^ unchecked((long)0xC4CEB9FE1A85EC53UL));
        double scale = 5.0 / size;

        BiomeType[] variants = _compatibleVariants.GetValueOrDefault(campaignBiome, [campaignBiome]);
        int varCount = variants.Length;

        BiomeType[] biomes = new BiomeType[size * size];
        for (int by = 0; by < size; by++)
        {
            for (int bx = 0; bx < size; bx++)
            {
                int idx = by * size + bx;

                if (alts[idx] == BattleMapAltitude.BelowWater)
                {
                    biomes[idx] = BiomeType.Coastal;
                    continue;
                }

                // Map noise [-1,1] → [0,1] → variant index.
                double d = (diversityNoise.Sample(bx * scale, by * scale) + 1.0) * 0.5;
                biomes[idx] = variants[(int)(d * varCount) % varCount];
            }
        }

        return biomes;
    }
}
