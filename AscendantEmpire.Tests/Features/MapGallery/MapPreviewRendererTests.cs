using AscendantEmpire.Application.MapGeneration;
using AscendantEmpire.Domain.Enums.Maps;

using SkiaSharp;

namespace AscendantEmpire.Tests.Features.MapGallery;

/// <summary>
/// Unit tests for <see cref="MapPreviewRenderer"/>.
/// </summary>
/// <remarks>
/// These tests exercise the SkiaSharp render path directly — no database or container required.
/// They confirm the native SkiaSharp binary loads, that both PNGs are produced, and that the
/// output dimensions and pixel colours match the expected tile-block layout and palette.
/// </remarks>
internal sealed class MapPreviewRendererTests
{
    private string _outputDirectory = null!;

    [SetUp]
    public void RendererSetUp()
    {
        // Render into an isolated temp directory so the test can assert files exist and clean up afterwards.
        _outputDirectory = Path.Combine(Path.GetTempPath(), "AscendantEmpire.Tests", Guid.NewGuid().ToString("N"));
    }

    [TearDown]
    public void RendererTearDown()
    {
        if (Directory.Exists(_outputDirectory))
        {
            Directory.Delete(_outputDirectory, recursive: true);
        }
    }

    [Test, Description("Render produces both PNGs on disk, sized to tile-blocks, with palette colours at known tiles.")]
    public void Render_ProducesBothPngs_WithCorrectDimensionsAndColours()
    {
        const int width = 3;
        const int height = 2;

        // Each tile is rendered as a 4x4 pixel block (private TilePixelSize in the renderer).
        const int tilePixelSize = 4;

        // Build a small geography array. Index layout is [y * width + x].
        // Tile (0,0) is DeepWater; everything else is Flat — gives us two distinct colours to sample.
        var geography = new CampaignMapGeographyType[width * height];
        Array.Fill(geography, CampaignMapGeographyType.Flat);
        geography[0] = CampaignMapGeographyType.DeepWater;

        // Build a matching biome array. Tile (0,0) is Ocean; everything else is Grassland.
        var biomes = new BiomeType[width * height];
        Array.Fill(biomes, BiomeType.Grassland);
        biomes[0] = BiomeType.Ocean;

        // Act.
        (string? geographyPath, string? biomePath) = MapPreviewRenderer.Render(
            geography, biomes, width, height, _outputDirectory, indexId: 42);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(geographyPath, Is.Not.Null.And.Not.Empty, "Geography path should be returned.");
            Assert.That(biomePath, Is.Not.Null.And.Not.Empty, "Biome path should be returned.");

            Assert.That(File.Exists(geographyPath!), Is.True, "Geography PNG should exist on disk.");
            Assert.That(File.Exists(biomePath!), Is.True, "Biome PNG should exist on disk.");

            Assert.That(new FileInfo(geographyPath!).Length, Is.GreaterThan(0), "Geography PNG should be non-empty.");
            Assert.That(new FileInfo(biomePath!).Length, Is.GreaterThan(0), "Biome PNG should be non-empty.");
        }

        // Decode the geography PNG and verify dimensions + palette colours at known tile centres.
        using SKBitmap geoBitmap = SKBitmap.Decode(geographyPath!);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(geoBitmap.Width, Is.EqualTo(width * tilePixelSize), "Geography PNG width should be width * tile size.");
            Assert.That(geoBitmap.Height, Is.EqualTo(height * tilePixelSize), "Geography PNG height should be height * tile size.");

            // Sample the centre pixel of tile (0,0) — expected DeepWater (20, 80, 140).
            SKColor deepWaterPixel = geoBitmap.GetPixel(1, 1);
            Assert.That((deepWaterPixel.Red, deepWaterPixel.Green, deepWaterPixel.Blue), Is.EqualTo(((byte)20, (byte)80, (byte)140)),
                "Tile (0,0) should be the DeepWater colour.");

            // Sample the centre pixel of tile (1,0) — expected Flat (100, 170, 80).
            SKColor flatPixel = geoBitmap.GetPixel(tilePixelSize + 1, 1);
            Assert.That((flatPixel.Red, flatPixel.Green, flatPixel.Blue), Is.EqualTo(((byte)100, (byte)170, (byte)80)),
                "Tile (1,0) should be the Flat colour.");
        }

        // Decode the biome PNG and verify a known tile colour.
        using SKBitmap biomeBitmap = SKBitmap.Decode(biomePath!);

        // Sample the centre pixel of tile (0,0) — expected Ocean (20, 80, 150).
        SKColor oceanPixel = biomeBitmap.GetPixel(1, 1);
        Assert.That((oceanPixel.Red, oceanPixel.Green, oceanPixel.Blue), Is.EqualTo(((byte)20, (byte)80, (byte)150)),
            "Tile (0,0) should be the Ocean colour.");
    }
}
