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

    // ── Rivers ────────────────────────────────────────────────────────────────

    /// <summary>
    /// How thick each river width is drawn, in pixels.
    /// </summary>
    /// <remarks>
    /// A tile is four pixels across here, so there is room for a one-pixel line and a two-pixel one and no more. Large is still
    /// twice Medium, which is the same relationship the map itself draws, so a river network's shape and rough size both read even
    /// at this scale. There is no pale casing under the stroke the way there is on the map: at one pixel there is nowhere to put it.
    /// </remarks>
    private static readonly Dictionary<RiverWidth, int> _riverThickness = new()
    {
        [RiverWidth.Medium] = 1,
        [RiverWidth.Large] = 2,
    };

    /// <summary>The colour rivers are drawn in, matching the campaign map's own so a preview and the map agree.</summary>
    private static readonly SKColor _riverColour = new(29, 100, 184);

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
    /// <param name="southRivers">
    /// The South river edge of each tile [y * width + x], or null to draw no rivers. Rivers run between tiles, and a tile owns only
    /// its South and East edges, so these two arrays are every edge on the map exactly once.
    /// </param>
    /// <param name="eastRivers">The East river edge of each tile [y * width + x], or null to draw no rivers.</param>
    public static (string? GeographyPath, string? BiomePath) Render(
        CampaignMapGeographyType[] geography,
        BiomeType[] biomes,
        int width,
        int height,
        string outputDirectory,
        int indexId,
        RiverWidth[]? southRivers = null,
        RiverWidth[]? eastRivers = null)
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
                (x, y) => _geoColours.GetValueOrDefault(geography[y * width + x], _geoColours[CampaignMapGeographyType.Unknown]),
                southRivers,
                eastRivers);

            RenderImage(
                width,
                height,
                biomePath,
                (x, y) => _biomeColours.GetValueOrDefault(biomes[y * width + x], _biomeColours[BiomeType.Unknown]),
                southRivers,
                eastRivers);

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
    /// Draws a red circle over an already-rendered base preview PNG at each start position, returning the
    /// resulting PNG as an in-memory byte array.
    /// </summary>
    /// <remarks>
    /// <para>
    /// This is rendered on demand (typically over the biome preview) rather than baked into the stored preview
    /// files, because start positions are authored and edited independently of the one-time map render — so the
    /// overlay must always reflect the current positions.
    /// </para>
    /// <para>
    /// Returns <c>null</c> if the base image cannot be decoded or rendering otherwise fails; callers should treat
    /// this as non-fatal (e.g. a 404). An empty <paramref name="startPositions"/> simply returns the base image.
    /// </para>
    /// </remarks>
    /// <param name="baseImagePath">Absolute path to the base preview PNG to draw the circles on top of.</param>
    /// <param name="startPositions">Tile coordinates (x, y) at which to draw a red circle.</param>
    public static byte[]? RenderStartPositionOverlay(string baseImagePath, IReadOnlyCollection<(int X, int Y)> startPositions)
    {
        try
        {
            // Decode the base preview straight from disk. A failure here (missing/corrupt file) returns null.
            using SKBitmap bitmap = SKBitmap.Decode(baseImagePath);

            if (bitmap is null)
            {
                return null;
            }

            // Draw directly onto the decoded bitmap's pixels.
            using var canvas = new SKCanvas(bitmap);

            // Radius scaled to the tile-block size so the filled dot is clearly visible on the 4px-per-tile
            // preview, with a small floor so it never collapses to a single pixel.
            float radius = Math.Max(TilePixelSize * 1.5f, 4f);

            using var paint = new SKPaint
            {
                Style = SKPaintStyle.Fill,
                Color = SKColors.Red,
                IsAntialias = true
            };

            foreach ((int x, int y) in startPositions)
            {
                // Centre the dot on the middle of the tile's pixel block (tiles render as TilePixelSize squares).
                float centreX = x * TilePixelSize + TilePixelSize / 2f;
                float centreY = y * TilePixelSize + TilePixelSize / 2f;
                canvas.DrawCircle(centreX, centreY, radius, paint);
            }

            canvas.Flush();

            // Encode the composited bitmap to PNG bytes — nothing is written to disk for the overlay.
            using SKImage image = SKImage.FromBitmap(bitmap);
            using SKData data = image.Encode(SKEncodedImageFormat.Png, 100);
            return data.ToArray();
        }
        catch
        {
            // Overlay rendering is best-effort — never throw; let the caller fall back to a not-found response.
            return null;
        }
    }

    // ─────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Writes a single PNG where each tile is a <see cref="TilePixelSize"/>-pixel square, with any rivers stroked over it.
    /// </summary>
    private static void RenderImage(
        int width,
        int height,
        string path,
        Func<int, int, SKColor> colourSelector,
        RiverWidth[]? southRivers,
        RiverWidth[]? eastRivers)
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

        DrawRivers(canvas, width, height, southRivers, eastRivers);

        canvas.Flush();

        // Snapshot the surface and encode to PNG, writing straight to disk.
        using SKImage image = surface.Snapshot();
        using SKData data = image.Encode(SKEncodedImageFormat.Png, 100);
        using FileStream stream = File.OpenWrite(path);
        data.SaveTo(stream);
    }

    /// <summary>
    /// Strokes the map's rivers along the tile boundaries they run on, over the tiles already drawn.
    /// </summary>
    /// <remarks>
    /// <para>
    /// A tile owns only its South and East edges, so drawing those two for every tile draws every edge on the map exactly once —
    /// no neighbour to consult and nothing to deduplicate.
    /// </para>
    /// <para>
    /// Drawn as rectangles aligned to whole pixels rather than as antialiased lines. A one-pixel line centred on a tile boundary
    /// falls between two pixel rows and comes out as two grey ones, which at this scale is the difference between a river and a
    /// smudge. Each stroke therefore starts one pixel before the boundary: Medium is that single pixel, and Large is it plus the
    /// one after, so the boundary itself always carries river colour.
    /// </para>
    /// </remarks>
    private static void DrawRivers(SKCanvas canvas, int width, int height, RiverWidth[]? southRivers, RiverWidth[]? eastRivers)
    {
        if (southRivers is null && eastRivers is null)
        {
            return;
        }

        using var paint = new SKPaint { Style = SKPaintStyle.Fill, Color = _riverColour, IsAntialias = false };

        for (int tileY = 0; tileY < height; tileY++)
        {
            for (int tileX = 0; tileX < width; tileX++)
            {
                int index = tileY * width + tileX;

                if (southRivers is not null && _riverThickness.TryGetValue(southRivers[index], out int southThickness))
                {
                    float boundaryY = (tileY + 1) * TilePixelSize;
                    canvas.DrawRect(tileX * TilePixelSize, boundaryY - 1, TilePixelSize, southThickness, paint);
                }

                if (eastRivers is not null && _riverThickness.TryGetValue(eastRivers[index], out int eastThickness))
                {
                    float boundaryX = (tileX + 1) * TilePixelSize;
                    canvas.DrawRect(boundaryX - 1, tileY * TilePixelSize, eastThickness, TilePixelSize, paint);
                }
            }
        }
    }
}
