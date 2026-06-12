using System.Data;
using System.Text.Json;

using AscendantEmpire.Application.Common;
using AscendantEmpire.Application.Data;
using AscendantEmpire.Application.MapGeneration;
using AscendantEmpire.Application.MapGeneration.Noise;
using AscendantEmpire.Application.Options;
using AscendantEmpire.Domain.Enums.Maps;

using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

using static AscendantEmpire.Application.Common.ServiceResultBase;

namespace AscendantEmpire.Application.Features.Maps.MapGallery.Handlers;

/// <summary>
/// Generates a campaign map using multi-octave Perlin noise with domain warping.
///
/// The full pipeline:
///   1. Parse generation parameters from PropertiesJson on the CampaignMapGalleryIndex.
///   2. Build a heightmap using domain-warped fBm Perlin noise.
///   3. Classify each tile's geography (Flat / Hilly / Mountainous / ShallowWater / DeepWater).
///   4. Assign a biome to each tile using temperature + moisture noise (Whittaker model).
///   5. Bulk-insert all tiles into mapgallery.CampaignMapGallery via SqlBulkCopy.
///   6. Render geography and biome preview PNGs; store their paths on the index record.
///   7. Write the resolved seed back to PropertiesJson so the map can be reproduced.
/// </summary>
internal sealed class GeneratePerlinNoiseCampaignMap(
    IServiceScopeFactory              scopeFactory,
    IOptions<MapGenerationSettings>   mapGenOptions
) : ICampaignMapGenerator
{
    // Parsed parameter fields — populated by ValidateRequestProperties before Generate is called.
    private long   _seed;
    private int    _waterRatio;
    private int    _hillAndMountainRatio;
    private bool   _poles;
    private bool   _equator;
    private int    _battleMapSize;

    // ──────────────────────────────────────────────────────────────────────────
    // Validation
    // ──────────────────────────────────────────────────────────────────────────

    public ServiceResultBase ValidateRequestProperties(
        Domain.Entities.Maps.CampaignMaps.CampaignMapGalleryIndex index)
    {
        Dictionary<string, string>? props =
            JsonSerializer.Deserialize<Dictionary<string, string>>(index.PropertiesJson);

        if (props is null)
        {
            return ServiceResultBase.Fail("PropertiesJson is missing or invalid for PerlinNoiseV1.");
        }

        // Seed: optional. If absent, generate a random one and store it back later.
        _seed = props.TryGetValue("seed", out var seedStr) && long.TryParse(seedStr, out var parsedSeed)
            ? parsedSeed
            : new Random().NextInt64();

        _waterRatio = props.TryGetValue("waterRatio", out var wrStr) && int.TryParse(wrStr, out var wr)
            ? Math.Clamp(wr, 0, 90)
            : 55;

        _hillAndMountainRatio = props.TryGetValue("hillAndMountainRatio", out var hrStr) && int.TryParse(hrStr, out var hr)
            ? Math.Clamp(hr, 0, 60)
            : 25;

        _poles = !props.TryGetValue("poles", out var polesStr) || !bool.TryParse(polesStr, out var poles) || poles;

        _equator = props.TryGetValue("equator", out var eqStr) && bool.TryParse(eqStr, out var eq) && eq;

        _battleMapSize = props.TryGetValue("battleMapSize", out var bmsStr) && int.TryParse(bmsStr, out var bms)
            ? Math.Max(10, bms)
            : 50;

        if (index.Width <= 0 || index.Height <= 0)
        {
            return ServiceResultBase.Fail("Map width and height must be positive.");
        }

        return ServiceResultBase.Succeed();
    }

    // ──────────────────────────────────────────────────────────────────────────
    // Generation
    // ──────────────────────────────────────────────────────────────────────────

    public async Task<ServiceResult<int>> GenerateCampaignMapAsync(
        Domain.Entities.Maps.CampaignMaps.CampaignMapGalleryIndex index)
    {
        int width  = index.Width;
        int height = index.Height;

        // ── Build the heightmap ─────────────────────────────────────────────
        // Each noise instance uses a distinct seed to ensure uncorrelated fields.
        var terrainNoise  = new PerlinNoise(_seed);
        var warpNoise     = new PerlinNoise(_seed ^ unchecked((long)0x9E3779B97F4A7C15UL));
        var ridgeNoise    = new PerlinNoise(_seed ^ unchecked((long)0x6C62272E07BB0142UL));
        var tempNoise     = new PerlinNoise(_seed ^ unchecked((long)0xD2A98B26625EEE7BUL));
        var moistureNoise = new PerlinNoise(_seed ^ unchecked((long)0xA3B195354A3937F2UL));

        var terrainFbm = new FractalBrownianMotion(terrainNoise)
        {
            Octaves     = 7,
            Persistence = 0.5,
            Lacunarity  = 2.0,
            Scale       = 3.0 / Math.Max(width, height),
        };

        var domainWarped = new DomainWarpedNoise(terrainFbm, warpNoise, doubleWarp: true)
        {
            WarpStrength = 0.15,
        };

        var ridgeFbm = new RidgedNoise(ridgeNoise)
        {
            Octaves    = 5,
            Lacunarity = 2.0,
            Gain       = 2.0,
            Scale      = 1.5 / Math.Max(width, height),
        };

        double[] heightmap = new double[width * height];
        double min = double.MaxValue, max = double.MinValue;
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
        if (range < 1e-10) { range = 1; }
        for (int i = 0; i < heightmap.Length; i++) { heightmap[i] = (heightmap[i] - min) / range; }

        // ── Geography classification ────────────────────────────────────────
        var geoSettings = new GeographySettings
        {
            WaterRatio              = _waterRatio,
            HillAndMountainRatio    = _hillAndMountainRatio,
            Poles                   = _poles,
            ShallowDepth            = 3,
            PolarReach              = 0.30,
            PolarDepth              = 0.60,
            RidgeInfluenceThreshold = 0.55,
            RidgeStrength           = 0.60,
            HarbourChance           = 0.08,
        };

        var geoGenerator = new GeographyGenerator(geoSettings);
        CampaignMapGeographyType[] geography = geoGenerator.Generate(heightmap, width, height, ridgeFbm);

        // ── Biome assignment ────────────────────────────────────────────────
        var biomeSettings = new BiomeSettings
        {
            Equator                = _equator,
            TemperatureNoiseWeight = 0.35,
            ClusteringPasses       = 2,
            ClusteringThreshold    = 0.60,
        };

        var biomeGenerator = new BiomeGenerator(biomeSettings);
        BiomeType[] biomes = biomeGenerator.Generate(
            geography, width, height, tempNoise, moistureNoise, geoOut: geography);

        // ── Bulk insert ─────────────────────────────────────────────────────
        using IServiceScope scope = scopeFactory.CreateScope();
        using var context = scope.ServiceProvider.GetRequiredService<AscendantEmpireContext>();

        // Determine the start ID using the same pattern as GenerateBlankCampaignMap.
        int startId = await GetNextStartIdAsync(context);

        await BulkInsertTilesAsync(context, index.Id, startId, width, height, geography, biomes);

        // ── Render preview PNGs ─────────────────────────────────────────────
        // Best-effort — if rendering fails the paths stay null, generation still succeeds.
        (index.GeographyMapPath, index.BiomeMapPath) = MapPreviewRenderer.Render(
            geography, biomes, width, height,
            mapGenOptions.Value.OutputDirectory,
            index.Id);

        // ── Write resolved seed back to PropertiesJson ──────────────────────
        var props = JsonSerializer.Deserialize<Dictionary<string, string>>(index.PropertiesJson)
                    ?? new Dictionary<string, string>();
        props["seed"] = _seed.ToString();
        index.PropertiesJson = JsonSerializer.Serialize(props);

        // index is tracked by the outer GenerateCampaignMapExecutor context, so
        // GeographyMapPath, BiomeMapPath, and PropertiesJson are all saved there.

        return ServiceResult<int>.Succeed(startId);
    }

    // ──────────────────────────────────────────────────────────────────────────
    // Database helpers
    // ──────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Reserves the next sequential starting ID in mapgallery.CampaignMapGallery,
    /// using HOLDLOCK to prevent races with concurrent map generation.
    /// </summary>
    private static async Task<int> GetNextStartIdAsync(AscendantEmpireContext context)
    {
        const string sql = """
            SELECT ISNULL(
                (SELECT MAX(CampaignMapGalleryId)
                 FROM [mapgallery].[CampaignMapGallery] WITH (HOLDLOCK, TABLOCKX)),
                0) + 1
            """;

        await using var conn = new SqlConnection(context.Database.GetConnectionString());
        await conn.OpenAsync();
        await using var cmd = new SqlCommand(sql, conn);
        object result = await cmd.ExecuteScalarAsync() ?? 1;
        return Convert.ToInt32(result);
    }

    /// <summary>
    /// Bulk-inserts all map tiles using SqlBulkCopy for maximum throughput.
    /// Tiles are ordered by (Y, X) to match the row-number convention used elsewhere.
    /// </summary>
    private static async Task BulkInsertTilesAsync(
        AscendantEmpireContext      context,
        int                         indexId,
        int                         startId,
        int                         width,
        int                         height,
        CampaignMapGeographyType[]  geography,
        BiomeType[]                 biomes)
    {
        // Build an in-memory DataTable matching the CampaignMapGallery schema.
        var table = new DataTable();
        table.Columns.Add("CampaignMapGalleryId",       typeof(int));
        table.Columns.Add("CampaignMapGalleryIndexId",  typeof(int));
        table.Columns.Add("XCoord",                     typeof(int));
        table.Columns.Add("YCoord",                     typeof(int));
        table.Columns.Add("BiomeTypeId",                typeof(int));
        table.Columns.Add("CampaignMapGeographyTypeId", typeof(int));

        int id = startId;
        // Iterate Y then X (row-major order) to match the ROW_NUMBER() ordering used elsewhere.
        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                int idx = y * width + x;
                table.Rows.Add(
                    id++,
                    indexId,
                    x,
                    y,
                    (int)biomes[idx],
                    (int)geography[idx]
                );
            }
        }

        string? connStr = context.Database.GetConnectionString();
        await using var conn = new SqlConnection(connStr);
        await conn.OpenAsync();

        using var bulkCopy = new SqlBulkCopy(conn, SqlBulkCopyOptions.TableLock, null)
        {
            DestinationTableName = "[mapgallery].[CampaignMapGallery]",
            BatchSize            = 10_000,
        };

        // Map DataTable column names to destination table column names.
        bulkCopy.ColumnMappings.Add("CampaignMapGalleryId",       "CampaignMapGalleryId");
        bulkCopy.ColumnMappings.Add("CampaignMapGalleryIndexId",  "CampaignMapGalleryIndexId");
        bulkCopy.ColumnMappings.Add("XCoord",                     "XCoord");
        bulkCopy.ColumnMappings.Add("YCoord",                     "YCoord");
        bulkCopy.ColumnMappings.Add("BiomeTypeId",                "BiomeTypeId");
        bulkCopy.ColumnMappings.Add("CampaignMapGeographyTypeId", "CampaignMapGeographyTypeId");

        await bulkCopy.WriteToServerAsync(table);
    }
}
