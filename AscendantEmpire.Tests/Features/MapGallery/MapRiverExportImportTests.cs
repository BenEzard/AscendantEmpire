using System.IO.Compression;
using System.Text.Json;
using System.Text.Json.Nodes;

using AscendantEmpire.Application.BackgroundServices;
using AscendantEmpire.Application.Data;
using AscendantEmpire.Application.Features.Maps.MapGallery.Classes.Export;
using AscendantEmpire.Application.Features.Maps.MapGallery.Executors;
using AscendantEmpire.Application.Options;
using AscendantEmpire.Domain.Entities.BackgroundProcessing;
using AscendantEmpire.Domain.Entities.Maps.CampaignMaps;
using AscendantEmpire.Domain.Enums.Maps;
using AscendantEmpire.Domain.Enums.Processing;
using AscendantEmpire.Domain.Enums.System;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace AscendantEmpire.Tests.Features.MapGallery;

/// <summary>
/// Rivers through export and import: that they survive a round trip, that a document without them still imports, and that a
/// document whose rivers break the authoring rules is refused.
/// </summary>
/// <remarks>
/// <para>
/// Containerised tier, because <see cref="MapImportExecutor"/> allocates tile ids inside an explicit transaction — see
/// <c>docs/testing.md</c>.
/// </para>
/// <para>
/// The map is <see cref="RiverMapSeed"/>'s: five wide, four high, sea along row 0. <c>East(0, 1)</c> is the vertical edge running
/// down from the coast at corner (1, 1), so it is the natural first stretch of a river.
/// </para>
/// </remarks>
internal sealed class MapRiverExportImportTests : SqlServerTestBase
{
    private string _tempDirectory = null!;

    [SetUp]
    public void SetUpTempDirectory()
    {
        _tempDirectory = Path.Combine(Path.GetTempPath(), "AscendantEmpireTests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_tempDirectory);
    }

    [TearDown]
    public void TearDownTempDirectory()
    {
        if (Directory.Exists(_tempDirectory))
        {
            Directory.Delete(_tempDirectory, recursive: true);
        }
    }

    [Test, Description("A map's rivers survive an export and re-import unchanged, on the same tiles and at the same widths.")]
    public async Task RoundTripPreservesRiversAsync()
    {
        int sourceIndexId = await RiverMapSeed.SeedAsync(DbFactory);

        // A river from the coast at corner (1, 1), down and then along, narrowing as it runs inland.
        await SetRiversAsync(sourceIndexId, (0, 1, RiverWidth.None, RiverWidth.Large), (1, 1, RiverWidth.Medium, RiverWidth.None));

        string zipPath = await ExportToZipAsync(sourceIndexId);
        int newIndexId = await ImportAsync(zipPath);

        await using AscendantEmpireContext verify = await DbFactory.CreateDbContextAsync();

        List<CampaignMapGallery> tiles = await verify.CampaignMapGallery.AsNoTracking()
            .Where(t => t.CampaignMapGalleryIndexId == newIndexId)
            .OrderBy(t => t.YCoord).ThenBy(t => t.XCoord)
            .ToListAsync();

        CampaignMapGallery mouthTile = tiles.Single(t => t.XCoord == 0 && t.YCoord == 1);
        CampaignMapGallery inlandTile = tiles.Single(t => t.XCoord == 1 && t.YCoord == 1);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(mouthTile.EastRiver, Is.EqualTo(RiverWidth.Large));
            Assert.That(inlandTile.SouthRiver, Is.EqualTo(RiverWidth.Medium));

            // Every other edge on the map comes back with no river, so the section is read as sparse rather than positional.
            Assert.That(tiles.Count(t => t.SouthRiver != RiverWidth.None || t.EastRiver != RiverWidth.None), Is.EqualTo(2));
        }
    }

    [Test, Description("The export names only the tiles carrying a river, so a section of two entries describes a twenty-tile map.")]
    public async Task ExportListsOnlyTilesThatCarryARiverAsync()
    {
        int sourceIndexId = await RiverMapSeed.SeedAsync(DbFactory);

        await SetRiversAsync(sourceIndexId, (0, 1, RiverWidth.None, RiverWidth.Large), (1, 1, RiverWidth.Medium, RiverWidth.None));

        string zipPath = await ExportToZipAsync(sourceIndexId);
        MapExportDocument document = await ReadDocumentAsync(zipPath);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(document.Rivers.Select(r => r.RelativeId), Is.EqualTo(new[] { 5, 6 }), "Row-major ids for (0, 1) and (1, 1) on a width-5 map.");
            Assert.That(document.Rivers.Single(r => r.RelativeId == 5).East, Is.EqualTo(RiverWidth.Large));
            Assert.That(document.Rivers.Single(r => r.RelativeId == 6).South, Is.EqualTo(RiverWidth.Medium));
            Assert.That(document.FormatVersion, Is.EqualTo("1.1"));
        }
    }

    [Test, Description("A map with no rivers exports an empty Rivers section rather than a row of Nones per tile.")]
    public async Task ExportOfARiverlessMapCarriesNoRiverEntriesAsync()
    {
        int sourceIndexId = await RiverMapSeed.SeedAsync(DbFactory);

        string zipPath = await ExportToZipAsync(sourceIndexId);
        MapExportDocument document = await ReadDocumentAsync(zipPath);

        Assert.That(document.Rivers, Is.Empty);
    }

    [Test, Description("A document written before rivers existed still imports, as a map with no rivers on it.")]
    public async Task ADocumentWithNoRiversSectionStillImportsAsync()
    {
        // A 1.0 export has no Rivers property at all, which is a different thing from an empty one and the case worth pinning.
        JsonObject json = JsonSerializer.SerializeToNode(BuildCoastalDocument(rivers: []))!.AsObject();
        json.Remove(nameof(MapExportDocument.Rivers));
        json[nameof(MapExportDocument.FormatVersion)] = "1.0";

        string zipPath = Path.Combine(_tempDirectory, "no-rivers.zip");
        await WriteZipAsync(json.ToJsonString(), zipPath);

        int newIndexId = await ImportAsync(zipPath);

        await using AscendantEmpireContext verify = await DbFactory.CreateDbContextAsync();

        List<CampaignMapGallery> tiles = await verify.CampaignMapGallery.AsNoTracking()
            .Where(t => t.CampaignMapGalleryIndexId == newIndexId)
            .ToListAsync();

        using (Assert.EnterMultipleScope())
        {
            Assert.That(tiles, Has.Count.EqualTo(RiverMapSeed.MAP_WIDTH * RiverMapSeed.MAP_HEIGHT));
            Assert.That(tiles.All(t => t.SouthRiver == RiverWidth.None && t.EastRiver == RiverWidth.None), Is.True);
        }
    }

    [Test, Description("An import runs the same authoring rules as the editor, so a river that never reaches the sea fails the whole import.")]
    public async Task ADocumentWithAnUnreachableRiverFailsTheImportAsync()
    {
        // (2, 2) is well inland of the sea along row 0, and no other edge connects it to the coast.
        MapExportDocument document = BuildCoastalDocument(rivers:
        [
            new MapExportRiverEdge { RelativeId = (2 * RiverMapSeed.MAP_WIDTH) + 2, South = RiverWidth.None, East = RiverWidth.Medium }
        ]);

        string zipPath = Path.Combine(_tempDirectory, "floating-river.zip");
        await WriteZipAsync(JsonSerializer.Serialize(document), zipPath);

        int processingId = await SeedImportProcessingAsync(zipPath);
        await RunImportAsync(processingId);

        await using AscendantEmpireContext verify = await DbFactory.CreateDbContextAsync();
        Processing persisted = await verify.Processing.AsNoTracking().SingleAsync(p => p.ProcessingId == processingId);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(persisted.Status, Is.EqualTo(ProcessingStatus.Failed));
            Assert.That(persisted.ErrorMessage, Does.Contain("does not reach the sea"));
            Assert.That(verify.CampaignMapGallery.Any(t => t.CampaignMapGalleryIndexId.ToString() == persisted.ResultReference), Is.False,
                "No tiles should have been written for a refused import.");
        }
    }

    [Test, Description("A river that widens as it runs inland fails the import, the same way the editor refuses it.")]
    public async Task ADocumentWithARiverWideningInlandFailsTheImportAsync()
    {
        MapExportDocument document = BuildCoastalDocument(rivers:
        [
            // Medium at the mouth, Large one hop further out.
            new MapExportRiverEdge { RelativeId = RiverMapSeed.MAP_WIDTH, South = RiverWidth.None, East = RiverWidth.Medium },
            new MapExportRiverEdge { RelativeId = RiverMapSeed.MAP_WIDTH + 1, South = RiverWidth.Large, East = RiverWidth.None }
        ]);

        string zipPath = Path.Combine(_tempDirectory, "widening-river.zip");
        await WriteZipAsync(JsonSerializer.Serialize(document), zipPath);

        int processingId = await SeedImportProcessingAsync(zipPath);
        await RunImportAsync(processingId);

        await using AscendantEmpireContext verify = await DbFactory.CreateDbContextAsync();
        Processing persisted = await verify.Processing.AsNoTracking().SingleAsync(p => p.ProcessingId == processingId);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(persisted.Status, Is.EqualTo(ProcessingStatus.Failed));
            Assert.That(persisted.ErrorMessage, Does.Contain("cannot run inland of"));
        }
    }

    // --- Helpers ----------------------------------------------------------------------------------

    /// <summary>Sets the two river columns on the named tiles of a seeded map.</summary>
    private async Task SetRiversAsync(int indexId, params (int X, int Y, RiverWidth South, RiverWidth East)[] edges)
    {
        await using AscendantEmpireContext context = await DbFactory.CreateDbContextAsync();

        foreach ((int x, int y, RiverWidth south, RiverWidth east) in edges)
        {
            CampaignMapGallery tile = await context.CampaignMapGallery
                .SingleAsync(t => t.CampaignMapGalleryIndexId == indexId && t.XCoord == x && t.YCoord == y);

            tile.SouthRiver = south;
            tile.EastRiver = east;
        }

        await context.SaveChangesAsync();
    }

    /// <summary>Runs the real exporter for the given map and returns the produced zip path.</summary>
    private async Task<string> ExportToZipAsync(int indexId)
    {
        await using AscendantEmpireContext context = await DbFactory.CreateDbContextAsync();

        Processing exportProcessing = new()
        {
            ProcessType = ProcessType.ExportGalleryMap,
            Status = ProcessingStatus.Pending,
            StartedAt = DateTime.UtcNow,
            MetadataJson = JsonSerializer.Serialize(new { mapGalleryIndexId = indexId })
        };

        context.Processing.Add(exportProcessing);
        await context.SaveChangesAsync();

        MapExportExecutor exporter = new(
            context,
            Options.Create(new ExportSettings { TempDirectory = _tempDirectory }),
            new NullLogger<MapExportExecutor>());

        await exporter.ExecuteAsync(exportProcessing, CancellationToken.None);

        Assert.That(exportProcessing.Status, Is.EqualTo(ProcessingStatus.Complete), () => exportProcessing.ErrorMessage ?? "export failed");

        return exportProcessing.ResultReference!;
    }

    /// <summary>Runs the importer for the staged zip and returns the new map's index id, asserting it completed.</summary>
    private async Task<int> ImportAsync(string zipPath)
    {
        int processingId = await SeedImportProcessingAsync(zipPath);

        await RunImportAsync(processingId);

        await using AscendantEmpireContext verify = await DbFactory.CreateDbContextAsync();
        Processing persisted = await verify.Processing.AsNoTracking().SingleAsync(p => p.ProcessingId == processingId);

        Assert.That(persisted.Status, Is.EqualTo(ProcessingStatus.Complete), () => persisted.ErrorMessage ?? "import failed");

        return int.Parse(persisted.ResultReference!);
    }

    private async Task RunImportAsync(int processingId)
    {
        await using AscendantEmpireContext runContext = await DbFactory.CreateDbContextAsync();

        Processing processing = await runContext.Processing.SingleAsync(p => p.ProcessingId == processingId);

        var channel = new BackgroundProcessingChannel(Options.Create(new BackgroundProcessingSettings()));
        MapImportExecutor executor = new(runContext, channel, new NullLogger<MapImportExecutor>());

        await executor.ExecuteAsync(processing, CancellationToken.None);
    }

    private async Task<int> SeedImportProcessingAsync(string zipPath)
    {
        await using AscendantEmpireContext context = await DbFactory.CreateDbContextAsync();

        Processing processing = new()
        {
            ProcessType = ProcessType.ImportGalleryMap,
            Status = ProcessingStatus.Pending,
            StartedAt = DateTime.UtcNow,
            MetadataJson = JsonSerializer.Serialize(new { importZipPath = zipPath })
        };

        context.Processing.Add(processing);
        await context.SaveChangesAsync();

        return processing.ProcessingId;
    }

    private static async Task<MapExportDocument> ReadDocumentAsync(string zipPath)
    {
        using ZipArchive archive = ZipFile.OpenRead(zipPath);

        await using Stream entryStream = archive.GetEntry("export.json")!.Open();
        using StreamReader reader = new(entryStream);

        return JsonSerializer.Deserialize<MapExportDocument>(await reader.ReadToEndAsync())!;
    }

    private static async Task WriteZipAsync(string json, string path)
    {
        await using FileStream fileStream = new(path, FileMode.Create, FileAccess.Write, FileShare.None);
        using ZipArchive archive = new(fileStream, ZipArchiveMode.Create, leaveOpen: false);

        ZipArchiveEntry entry = archive.CreateEntry("export.json", CompressionLevel.Optimal);
        await using Stream entryStream = entry.Open();
        await using StreamWriter writer = new(entryStream);
        await writer.WriteAsync(json);
    }

    /// <summary>
    /// A hand-crafted document for the same coastal map <see cref="RiverMapSeed"/> seeds: sea along row 0, land below it.
    /// </summary>
    private static MapExportDocument BuildCoastalDocument(IReadOnlyList<MapExportRiverEdge> rivers)
    {
        return new MapExportDocument
        {
            ExportedAt = DateTime.UtcNow,
            Map = new MapExportIndex
            {
                MapName = "Imported Coast",
                Description = "hand-crafted",
                Width = RiverMapSeed.MAP_WIDTH,
                Height = RiverMapSeed.MAP_HEIGHT,
                CreationEngine = MapCreationEngine.BlankMap,
                HorizontalWrap = false,
                VerticalWrap = false,
                PropertiesJson = "{}",
                CampaignMapOffsetId = 999 // deliberately bogus: import must reassign this.
            },
            Tiles = [.. Enumerable.Range(0, RiverMapSeed.MAP_WIDTH * RiverMapSeed.MAP_HEIGHT).Select(relativeId => new MapExportTile
            {
                RelativeId = relativeId,
                BiomeTypeId = relativeId < RiverMapSeed.MAP_WIDTH ? BiomeType.Ocean : BiomeType.Grassland,
                GeographyTypeId = CampaignMapGeographyType.Flat
            })],
            Biomes =
            [
                new MapExportBiome
                {
                    BiomeTypeId = BiomeType.Grassland,
                    Name = "Grassland",
                    MovementCost = 1.0m,
                    DoesBlockSight = false,
                    IsWater = false,
                    IsTraversable = true,
                    RegenerationPercent = 0.1m,
                    IsPaintableOnCampaignMap = true,
                    MapEditorHtmlColor = "#90ee90",
                    CanBeElevated = true
                },
                new MapExportBiome
                {
                    BiomeTypeId = BiomeType.Ocean,
                    Name = "Ocean",
                    MovementCost = 1.0m,
                    DoesBlockSight = false,
                    IsWater = true,
                    IsTraversable = false,
                    RegenerationPercent = 0.1m,
                    IsPaintableOnCampaignMap = true,
                    MapEditorHtmlColor = "#0000ff",
                    CanBeElevated = false
                }
            ],
            Geographies =
            [
                new MapExportGeography
                {
                    GeographyTypeId = CampaignMapGeographyType.Flat,
                    Name = "Flat",
                    RelativeElevation = 0,
                    MovementCost = 1.0m,
                    MapEditorIcon = null,
                    IsPaintableOnCampaignMap = true
                }
            ],
            StartPositions = [],
            Rivers = rivers
        };
    }
}
