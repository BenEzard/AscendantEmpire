using AscendantEmpire.Domain.Enums.Maps;

using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;

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

    private static readonly Dictionary<CampaignMapGeographyType, Rgb24> _geoColours = new()
    {
        [CampaignMapGeographyType.DeepWater]    = new Rgb24(20,  80,  140),
        [CampaignMapGeographyType.ShallowWater] = new Rgb24(60,  150, 210),
        [CampaignMapGeographyType.Flat]         = new Rgb24(100, 170, 80),
        [CampaignMapGeographyType.Hilly]        = new Rgb24(170, 145, 80),
        [CampaignMapGeographyType.Mountainous]  = new Rgb24(160, 150, 145),
        [CampaignMapGeographyType.Unknown]      = new Rgb24(20,  20,  20),
    };

    // ── Biome colour palette ──────────────────────────────────────────────────

    private static readonly Dictionary<BiomeType, Rgb24> _biomeColours = new()
    {
        [BiomeType.Ocean]     = new Rgb24(20,  80,  150),
        [BiomeType.Coastal]   = new Rgb24(90,  175, 200),
        [BiomeType.Grassland] = new Rgb24(105, 185, 80),
        [BiomeType.Woodland]  = new Rgb24(80,  140, 65),
        [BiomeType.Forest]    = new Rgb24(45,  110, 50),
        [BiomeType.Jungle]    = new Rgb24(25,  135, 55),
        [BiomeType.Arid]      = new Rgb24(195, 175, 90),
        [BiomeType.Desert]    = new Rgb24(225, 205, 120),
        [BiomeType.Tundra]    = new Rgb24(165, 185, 170),
        [BiomeType.Ice]       = new Rgb24(225, 238, 248),
        [BiomeType.Unknown]   = new Rgb24(20,  20,  20),
        [BiomeType.Void]      = new Rgb24(20,  20,  20),
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
    private static void RenderImage(int width, int height, string path, Func<int, int, Rgb24> colourSelector)
    {
        int imgWidth  = width  * TilePixelSize;
        int imgHeight = height * TilePixelSize;

        using var image = new Image<Rgb24>(imgWidth, imgHeight);

        // ImageSharp ProcessPixelRows is the preferred high-performance path.
        image.ProcessPixelRows(accessor =>
        {
            for (int py = 0; py < imgHeight; py++)
            {
                // Map pixel row → tile row.
                int tileY = py / TilePixelSize;
                Span<Rgb24> row = accessor.GetRowSpan(py);

                for (int px = 0; px < imgWidth; px++)
                {
                    int tileX = px / TilePixelSize;
                    row[px] = colourSelector(tileX, tileY);
                }
            }
        });

        image.SaveAsPng(path);
    }
}
