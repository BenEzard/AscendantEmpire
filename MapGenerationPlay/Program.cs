using System.Globalization;
using MapGenerationPlay.Generation;
using MapGenerationPlay.Noise;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;

// ──────────────────────────────────────────────────────────────────────────────
// MapGenerationPlay — prototype console app for the Perlin Noise map generator.
//
// Usage:
//   MapGenerationPlay [--count N] [--width W] [--height H] [--seed S]
//                     [--waterRatio R] [--hillRatio R] [--poles true|false]
//                     [--outDir path]
//
// Defaults: count=5, width=120, height=80, random seeds, waterRatio=55,
//           hillRatio=25, poles=true, outDir=output
// ──────────────────────────────────────────────────────────────────────────────

// "args" is the implicit top-level parameter (string[]) — we parse it into "options".
var options = Args.Parse(args);

string outDir = options.OutDir;
Directory.CreateDirectory(outDir);

var rng = new Random();
int count = options.Count;

Console.WriteLine($"Generating {count} map(s) → {Path.GetFullPath(outDir)}");
Console.WriteLine($"  Size:          {options.Width} × {options.Height}");
Console.WriteLine($"  WaterRatio:    {options.WaterRatio}%");
Console.WriteLine($"  HillRatio:     {options.HillRatio}%");
Console.WriteLine($"  Poles:         {options.Poles}");
Console.WriteLine();

for (int i = 0; i < count; i++)
{
    long seed = options.Seed ?? rng.NextInt64();
    MapResult result = GenerateMap(seed, options.Width, options.Height,
                                   options.WaterRatio, options.HillRatio, options.Poles);

    string baseName = $"map_seed_{seed}_{options.Width}x{options.Height}";

    SaveHeightmapPng(result.Heightmap, options.Width, options.Height,
                     Path.Combine(outDir, baseName + "_heightmap.png"));
    SaveHeightmapCsv(result.Heightmap, options.Width, options.Height,
                     Path.Combine(outDir, baseName + "_heightmap.csv"));
    SaveGeographyPng(result.Geography, options.Width, options.Height,
                     Path.Combine(outDir, baseName + "_geography.png"));
    SaveBiomePng(result.Biomes, options.Width, options.Height,
                 Path.Combine(outDir, baseName + "_biomes.png"));

    // Generate two adjacent BattleMaps for a Flat or Hilly tile, searching outward
    // from the map centre until we find one (skips water / mountain tiles).
    (int bcx, int bcy) = FindLandTileNearCentre(result.Geography, options.Width, options.Height);
    if (bcx >= 0)
    {
        GenerateAndSaveBattleMaps(seed, options.Width, options.Height, bcx, bcy,
                                  result.Geography, outDir, baseName);
    }

    Console.WriteLine($"  [{i + 1}/{count}] seed={seed}  → {baseName}");
}

Console.WriteLine("\nDone.");

// ──────────────────────────────────────────────────────────────────────────────
// Map generation
// ──────────────────────────────────────────────────────────────────────────────

static MapResult GenerateMap(
    long   seed,
    int    width,
    int    height,
    int    waterRatio,
    int    hillRatio,
    bool   poles)
{
    // ── Terrain heightmap ────────────────────────────────────────────────────
    // Each noise instance must use a distinct seed so the fields are uncorrelated.
    // XOR with well-distributed constants avoids trivial cancellation.
    var terrainNoise  = new PerlinNoise(seed);
    var warpNoise     = new PerlinNoise(seed ^ unchecked((long)0x9E3779B97F4A7C15UL));   // golden-ratio mix
    var ridgeNoise    = new PerlinNoise(seed ^ unchecked((long)0x6C62272E07BB0142UL));   // FNV-like mix
    var tempNoise     = new PerlinNoise(seed ^ unchecked((long)0xD2A98B26625EEE7BUL));   // separate temperature field
    var moistureNoise = new PerlinNoise(seed ^ unchecked((long)0xA3B195354A3937F2UL));   // separate moisture field

    var terrainFbm = new FractalBrownianMotion(terrainNoise)
    {
        Octaves     = 7,
        Persistence = 0.5,
        Lacunarity  = 2.0,
        // Scale: controls how many noise cycles fit across the map.
        // Dividing by max(w,h) normalises so the scale feels the same for any map size.
        Scale = 3.0 / Math.Max(width, height),
    };

    // Domain warp: offset (x,y) by a second noise field before sampling terrain.
    // WarpStrength of 0.15 gives clearly irregular coastlines without looking smeared.
    var domainWarped = new DomainWarpedNoise(terrainFbm, warpNoise, doubleWarp: true)
    {
        WarpStrength = 0.15,
    };

    // Ridged noise for mountain ranges. Coarser scale than terrain so ranges are
    // large-scale features, not tile-by-tile speckle.
    var ridgeFbm = new RidgedNoise(ridgeNoise)
    {
        Octaves    = 5,
        Lacunarity = 2.0,
        Gain       = 2.0,
        Scale      = 1.5 / Math.Max(width, height),
    };

    // Sample raw heightmap.
    double[] heightmap = new double[width * height];
    double min = double.MaxValue;
    double max = double.MinValue;

    for (int y = 0; y < height; y++)
    {
        for (int x = 0; x < width; x++)
        {
            double h = domainWarped.Sample(x, y);
            heightmap[y * width + x] = h;
            if (h < min) { min = h; }
            if (h > max) { max = h; }
        }
    }

    // Normalise to [0, 1].
    double range = max - min;
    if (range < 1e-10) { range = 1; }   // guard against a theoretically flat map
    for (int i = 0; i < heightmap.Length; i++)
    {
        heightmap[i] = (heightmap[i] - min) / range;
    }

    // ── Geography classification ──────────────────────────────────────────────
    var geoSettings = new GeographySettings
    {
        WaterRatio           = waterRatio,
        HillAndMountainRatio = hillRatio,
        Poles                = poles,
        ShallowDepth         = 3,
        PolarReach           = 0.30,
        PolarDepth           = 0.60,
        RidgeInfluenceThreshold = 0.55,
        RidgeStrength        = 0.60,
        HarbourChance        = 0.08,
    };

    var geoGenerator = new GeographyGenerator(geoSettings);
    GeographyType[] geography = geoGenerator.Generate(heightmap, width, height, ridgeFbm);

    // ── Biome assignment ──────────────────────────────────────────────────────
    // geography is passed as geoOut — the biome generator may reclassify Hilly/Mountainous
    // to Flat where CanBeElevated is violated (modifies the geography array in place).
    var biomeSettings = new BiomeSettings
    {
        Equator                  = false,
        TemperatureNoiseWeight   = 0.35,
        ClusteringPasses         = 2,
        ClusteringThreshold      = 0.60,
    };

    var biomeGenerator = new BiomeGenerator(biomeSettings);
    BiomeType[] biomes = biomeGenerator.Generate(
        geography, width, height,
        tempNoise, moistureNoise,
        geoOut: geography);

    return new MapResult(heightmap, geography, biomes);
}

// ──────────────────────────────────────────────────────────────────────────────
// Output helpers
// ──────────────────────────────────────────────────────────────────────────────

static void SaveHeightmapPng(double[] heightmap, int width, int height, string path)
{
    using var image = new Image<L8>(width, height);
    image.ProcessPixelRows(accessor =>
    {
        for (int y = 0; y < height; y++)
        {
            var row = accessor.GetRowSpan(y);
            for (int x = 0; x < width; x++)
            {
                byte luma = (byte)(heightmap[y * width + x] * 255);
                row[x] = new L8(luma);
            }
        }
    });
    image.SaveAsPng(path);
}

static void SaveHeightmapCsv(double[] heightmap, int width, int height, string path)
{
    using var writer = new StreamWriter(path);
    for (int y = 0; y < height; y++)
    {
        for (int x = 0; x < width; x++)
        {
            if (x > 0) { writer.Write(','); }
            writer.Write(heightmap[y * width + x].ToString("F4", CultureInfo.InvariantCulture));
        }
        writer.WriteLine();
    }
}

/// <summary>
/// Colour-coded geography PNG:
///   DeepWater    → dark blue  (#1a3a6b)
///   ShallowWater → teal       (#5abcb4)
///   Flat         → green      (#6aaa3c)
///   Hilly        → tan/yellow (#c8a847)
///   Mountainous  → grey       (#8a8a8a)
/// </summary>
static void SaveGeographyPng(GeographyType[] geography, int width, int height, string path)
{
    using var image = new Image<Rgb24>(width, height);
    image.ProcessPixelRows(accessor =>
    {
        for (int y = 0; y < height; y++)
        {
            var row = accessor.GetRowSpan(y);
            for (int x = 0; x < width; x++)
            {
                row[x] = geography[y * width + x] switch
                {
                    GeographyType.DeepWater    => new Rgb24(0x1a, 0x3a, 0x6b),
                    GeographyType.ShallowWater => new Rgb24(0x5a, 0xbc, 0xb4),
                    GeographyType.Flat         => new Rgb24(0x6a, 0xaa, 0x3c),
                    GeographyType.Hilly        => new Rgb24(0xc8, 0xa8, 0x47),
                    GeographyType.Mountainous  => new Rgb24(0x8a, 0x8a, 0x8a),
                    _                          => new Rgb24(0xff, 0x00, 0xff),  // magenta = bug
                };
            }
        }
    });
    image.SaveAsPng(path);
}

/// <summary>
/// Searches outward from the map centre using a BFS-like spiral to find a Flat or Hilly
/// campaign tile (non-water, non-mountainous) that also has a valid right-neighbour
/// so two adjacent BattleMaps can be generated.
/// Returns (-1, -1) if none found.
/// </summary>
static (int x, int y) FindLandTileNearCentre(GeographyType[] geo, int w, int h)
{
    int cx = w / 2, cy = h / 2;
    int maxR = Math.Max(w, h);

    for (int r = 0; r <= maxR; r++)
    {
        for (int dy = -r; dy <= r; dy++)
        {
            for (int dx = -r; dx <= r; dx++)
            {
                if (Math.Abs(dx) != r && Math.Abs(dy) != r) { continue; } // only shell
                int x = cx + dx, y = cy + dy;
                if (x < 0 || x + 1 >= w || y < 0 || y >= h) { continue; }
                var g = geo[y * w + x];
                var gr = geo[y * w + x + 1];
                if (g is GeographyType.Flat or GeographyType.Hilly
                    && gr is GeographyType.Flat or GeographyType.Hilly)
                {
                    return (x, y);
                }
            }
        }
    }
    return (-1, -1);
}

/// <summary>
/// Generates two horizontally adjacent BattleMaps and saves altitude + biome PNGs.
/// Producing adjacent tiles lets us visually verify that their shared edge is seamless.
/// </summary>
static void GenerateAndSaveBattleMaps(
    long            masterSeed,
    int             campaignWidth,
    int             campaignHeight,
    int             camX,
    int             camY,
    GeographyType[] campaignGeo,
    string          outDir,
    string          baseName)
{
    const int battleSize = 50;
    var gen = new BattleMapGenerator();

    GeographyType GetGeo(int cx, int cy)
    {
        if (cx < 0 || cx >= campaignWidth || cy < 0 || cy >= campaignHeight)
        {
            return GeographyType.DeepWater;  // outside the map = ocean
        }
        return campaignGeo[cy * campaignWidth + cx];
    }

    GeographyType tileGeo0 = GetGeo(camX, camY);
    GeographyType tileGeo1 = GetGeo(camX + 1, camY);

    // Tile 0 neighbours: Left, Right (= tile 1), Up, Down
    var neighbours0 = new GeographyType?[]
    {
        GetGeo(camX - 1, camY),
        tileGeo1,
        GetGeo(camX, camY - 1),
        GetGeo(camX, camY + 1),
    };

    // Tile 1 neighbours: Left (= tile 0), Right, Up, Down
    var neighbours1 = new GeographyType?[]
    {
        tileGeo0,
        GetGeo(camX + 2, camY),
        GetGeo(camX + 1, camY - 1),
        GetGeo(camX + 1, camY + 1),
    };

    // Use masterSeed as the gameSeed placeholder in the play project (no GameIndex here).
    BattleMapResult bm0 = gen.Generate(masterSeed, masterSeed, camX,     camY, battleSize, campaignWidth, campaignHeight, tileGeo0, neighbours0);
    BattleMapResult bm1 = gen.Generate(masterSeed, masterSeed, camX + 1, camY, battleSize, campaignWidth, campaignHeight, tileGeo1, neighbours1);

    SaveAltitudePng(bm0.Altitudes, battleSize, battleSize,
                    Path.Combine(outDir, baseName + $"_battle_{camX}_{camY}_altitude.png"));
    SaveAltitudePng(bm1.Altitudes, battleSize, battleSize,
                    Path.Combine(outDir, baseName + $"_battle_{camX + 1}_{camY}_altitude.png"));

    // Stitch both tiles side-by-side for easy comparison of the shared edge.
    SaveAltitudePngStitched(bm0.Altitudes, bm1.Altitudes, battleSize,
                            Path.Combine(outDir, baseName + $"_battle_{camX}_{camY}_adjacent_stitched.png"));
}

/// <summary>
/// Altitude map PNG where each level gets a distinct colour:
///   BelowWater(-1) = dark blue, Ground(0) = green, Low(1) = yellow-green,
///   Medium(2) = orange, High(3) = red-brown, Peak(4) = white.
/// </summary>
static void SaveAltitudePng(BattleMapAltitude[] altitudes, int width, int height, string path)
{
    using var image = new Image<Rgb24>(width, height);
    image.ProcessPixelRows(accessor =>
    {
        for (int y = 0; y < height; y++)
        {
            var row = accessor.GetRowSpan(y);
            for (int x = 0; x < width; x++)
            {
                row[x] = altitudes[y * width + x] switch
                {
                    BattleMapAltitude.BelowWater => new Rgb24(0x1a, 0x3a, 0x6b),
                    BattleMapAltitude.Ground     => new Rgb24(0x6a, 0xaa, 0x3c),
                    BattleMapAltitude.Low        => new Rgb24(0xc8, 0xd0, 0x40),
                    BattleMapAltitude.Medium     => new Rgb24(0xd0, 0x80, 0x20),
                    BattleMapAltitude.High       => new Rgb24(0xa0, 0x40, 0x10),
                    BattleMapAltitude.Peak       => new Rgb24(0xf0, 0xf0, 0xf0),
                    _                            => new Rgb24(0xff, 0x00, 0xff),
                };
            }
        }
    });
    image.SaveAsPng(path);
}

/// <summary>
/// Saves two adjacent BattleMaps side-by-side to make the shared edge visible.
/// A 2px red divider is drawn at the seam so the eye can judge continuity.
/// </summary>
static void SaveAltitudePngStitched(
    BattleMapAltitude[] left, BattleMapAltitude[] right, int size, string path)
{
    int totalWidth = size * 2 + 2;  // 2px divider
    using var image = new Image<Rgb24>(totalWidth, size);
    image.ProcessPixelRows(accessor =>
    {
        for (int y = 0; y < size; y++)
        {
            var row = accessor.GetRowSpan(y);

            // Left tile
            for (int x = 0; x < size; x++)
            {
                row[x] = AltitudeColour(left[y * size + x]);
            }
            // Red divider
            row[size]     = new Rgb24(0xff, 0x00, 0x00);
            row[size + 1] = new Rgb24(0xff, 0x00, 0x00);
            // Right tile
            for (int x = 0; x < size; x++)
            {
                row[size + 2 + x] = AltitudeColour(right[y * size + x]);
            }
        }
    });
    image.SaveAsPng(path);
}

static Rgb24 AltitudeColour(BattleMapAltitude alt) => alt switch
{
    BattleMapAltitude.BelowWater => new Rgb24(0x1a, 0x3a, 0x6b),
    BattleMapAltitude.Ground     => new Rgb24(0x6a, 0xaa, 0x3c),
    BattleMapAltitude.Low        => new Rgb24(0xc8, 0xd0, 0x40),
    BattleMapAltitude.Medium     => new Rgb24(0xd0, 0x80, 0x20),
    BattleMapAltitude.High       => new Rgb24(0xa0, 0x40, 0x10),
    BattleMapAltitude.Peak       => new Rgb24(0xf0, 0xf0, 0xf0),
    _                            => new Rgb24(0xff, 0x00, 0xff),
};

/// <summary>
/// Colour-coded biome PNG.
/// Colours are loosely representative of real-world biome appearance.
/// </summary>
static void SaveBiomePng(BiomeType[] biomes, int width, int height, string path)
{
    using var image = new Image<Rgb24>(width, height);
    image.ProcessPixelRows(accessor =>
    {
        for (int y = 0; y < height; y++)
        {
            var row = accessor.GetRowSpan(y);
            for (int x = 0; x < width; x++)
            {
                row[x] = biomes[y * width + x] switch
                {
                    BiomeType.Ocean     => new Rgb24(0x1a, 0x3a, 0x6b),  // dark blue
                    BiomeType.Coastal   => new Rgb24(0x5a, 0xbc, 0xb4),  // teal
                    BiomeType.Ice       => new Rgb24(0xe8, 0xf0, 0xff),  // near-white
                    BiomeType.Tundra    => new Rgb24(0x9b, 0xab, 0x9c),  // grey-green
                    BiomeType.Grassland => new Rgb24(0x7e, 0xc8, 0x50),  // bright green
                    BiomeType.Forest    => new Rgb24(0x2d, 0x6e, 0x2a),  // dark green
                    BiomeType.Jungle    => new Rgb24(0x1a, 0x4d, 0x1a),  // very dark green
                    BiomeType.Woodland  => new Rgb24(0x5a, 0x8a, 0x3c),  // medium green
                    BiomeType.Arid      => new Rgb24(0xc8, 0xb0, 0x60),  // light tan
                    BiomeType.Desert    => new Rgb24(0xe8, 0xd2, 0x7a),  // sandy yellow
                    _                   => new Rgb24(0xff, 0x00, 0xff),  // magenta = bug
                };
            }
        }
    });
    image.SaveAsPng(path);
}

// ──────────────────────────────────────────────────────────────────────────────
// Result type
// ──────────────────────────────────────────────────────────────────────────────

/// <summary>
/// Holds all generated map layers.
/// </summary>
record MapResult(double[] Heightmap, GeographyType[] Geography, BiomeType[] Biomes);

// ──────────────────────────────────────────────────────────────────────────────
// CLI argument parsing
// ──────────────────────────────────────────────────────────────────────────────

record Args(int Count, int Width, int Height, long? Seed, int WaterRatio, int HillRatio, bool Poles, string OutDir)
{
    public static Args Parse(string[] argv)
    {
        int    count      = 5;
        int    width      = 120;
        int    height     = 80;
        long?  seed       = null;
        int    waterRatio = 55;
        int    hillRatio  = 25;
        bool   poles      = true;
        string outDir     = "output";

        for (int i = 0; i < argv.Length; i++)
        {
            switch (argv[i])
            {
                case "--count":      count      = int.Parse(argv[++i]);                    break;
                case "--width":      width      = int.Parse(argv[++i]);                    break;
                case "--height":     height     = int.Parse(argv[++i]);                    break;
                case "--seed":       seed       = long.Parse(argv[++i]);                   break;
                case "--waterRatio": waterRatio = int.Parse(argv[++i]);                    break;
                case "--hillRatio":  hillRatio  = int.Parse(argv[++i]);                    break;
                case "--poles":      poles      = bool.Parse(argv[++i]);                   break;
                case "--outDir":     outDir     = argv[++i];                               break;
                default:
                    Console.Error.WriteLine($"Unknown argument: {argv[i]}");
                    break;
            }
        }

        return new Args(count, width, height, seed, waterRatio, hillRatio, poles, outDir);
    }
}
