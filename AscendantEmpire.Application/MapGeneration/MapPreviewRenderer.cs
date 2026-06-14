using AscendantEmpire.Domain.Enums.Maps;

using SkiaSharp;

namespace AscendantEmpire.Application.MapGeneration;

/// <summary>
/// Renders campaign-map geography and biome arrays to PNG preview files.
///
/// Each tile maps to a <see cref="TilePixelSize"/>×<see cref="TilePixelSize"/> block of
/// pixels, so small maps are still comfortably viewable without a nearest-neighbour zoom.
///
/// Call <see cref="Render"/> immediately after generation; it returns the two file paths
/// which should be stored on the <c>CampaignMapGalleryIndex</c> record.
/// </summary>
public static class MapPreviewRenderer
{
    /// <summary>Side length (in pixels) of each campaign tile in the output PNG.</summary>
    private const int TilePixelSize = 4;

    // ── Geography colour palette ──────────────────────────────────────────────

    private static readonly Dictionary<CampaignMapGeographyType, SKColor> _geoColours = new()
    {
        [CampaignMapGeographyType.DeepWater]    = new SKColor(20,  80,  140),
        [CampaignMapGeographyType.ShallowWater] = new SKColor(60,  150, 210),
        [CampaignMapGeographyType.Flat]         = new SKColor(100, 170, 80),
        [CampaignMapGeographyType.Hilly]        = new SKColor(170, 145, 80),
        [CampaignMapGeographyType.Mountainous]  = new SKColor(160, 150, 145),
        [CampaignMapGeographyType.Unknown]      = new SKColor(20,  20,  20),
    };

    // ── Biome colour palette ──────────────────────────────────────────────────

    private static readonly Dictionary<BiomeType, SKColor> _biomeColours = new()
    {
        [BiomeType.Ocean]     = new SKColor(20,  80,  150),
        [BiomeType.Coastal]   = new SKColor(90,  175, 200),
        [BiomeType.Grassland] = new SKColor(105, 185, 80),
        [BiomeType.Woodland]  = new SKColor(80,  140, 65),
        [BiomeType.Forest]    = new SKColor(45,  110, 50),
        [BiomeType.Jungle]    = new SKColor(25,  135, 55),
        [BiomeType.Arid]      = new SKColor(195, 175, 90),
        [BiomeType.Desert]    = new SKColor(225, 205, 120),
        [BiomeType.Tundra]    = new SKColor(165, 185, 170),
        [BiomeType.Ice]       = new SKColor(225, 238, 248),
        [BiomeType.Unknown]   = new SKColor(20,  20,  20),
        [BiomeType.Void]      = new SKColor(20,  20,  20),
    };

    // ─────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Renders both preview PNGs and returns their absolute paths.
    /// Returns <c>(null, null)</c> if rendering fails — callers should treat
    /// this as non-fatal and continue without paths.
    /// </summary>
    /// <param name="geography">Geography array [y * width + x].</param>
    /// <param name="biomes">Biome array [y * width + x].</param>
    /// <param name="width">Map width in tiles.</param>
    /// <param name="height">Map height in tiles.</param>
    /// <param name="outputDirectory">Directory to write PNGs into (created if missing).</param>
    /// <param name="indexId">Used to build unique, stable filenames.</param>
    public static (string? GeographyPath, string? BiomePath) Render(
        CampaignMapGeographyType[] geography,
        BiomeType[] biomes,
        int width,
        int height,
        string outputDirectory,
        int indexId)
    {
        try
        {
            Directory.CreateDirectory(outputDirectory);

            string geoPath  = Path.Combine(outputDirectory, $"{indexId}_geography.png");
            string biomePath = Path.Combine(outputDirectory, $"{indexId}_biomes.png");

            RenderImage(
                width,
                height,
                geoPath,
                (x, y) => _geoColours.GetValueOrDefault(geography[y * width + x], _geoColours[CampaignMapGeographyType.Unknown]));

            RenderImage(
                width,
                height,
                biomePath,
                (x, y) => _biomeColours.GetValueOrDefault(biomes[y * width + x], _biomeColours[BiomeType.Unknown]));

            return (geoPath, biomePath);
        }
        catch
        {
            // Rendering is best-effort — never fail the map generation because of a PNG.
            return (null, null);
        }
    }

    // ─────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Writes a single PNG where each tile is a <see cref="TilePixelSize"/>-pixel square.
    /// </summary>
    private static void RenderImage(int width, int height, string path, Func<int, int, SKColor> colourSelector)
    {
        int imgWidth  = width  * TilePixelSize;
        int imgHeight = height * TilePixelSize;

        // Opaque RGB surface — no alpha channel needed for solid map previews.
        using var surface = SKSurface.Create(new SKImageInfo(imgWidth, imgHeight, SKColorType.Rgba8888, SKAlphaType.Opaque));
        SKCanvas canvas = surface.Canvas;

        // Draw one filled square per tile rather than setting individual pixels —
        // far fewer draw calls, and it matches the "block per tile" intent directly.
        using var paint = new SKPaint { Style = SKPaintStyle.Fill, IsAntialias = false };

        for (int tileY = 0; tileY < height; tileY++)
        {
            for (int tileX = 0; tileX < width; tileX++)
            {
                paint.Color = colourSelector(tileX, tileY);

                float left = tileX * TilePixelSize;
                float top  = tileY * TilePixelSize;
                canvas.DrawRect(left, top, TilePixelSize, TilePixelSize, paint);
            }
        }

        canvas.Flush();

        // Snapshot the surface and encode to PNG, writing straight to disk.
        using SKImage image = surface.Snapshot();
        using SKData data = image.Encode(SKEncodedImageFormat.Png, 100);
        using FileStream stream = File.OpenWrite(path);
        data.SaveTo(stream);
    }
}
