using System.IO.Compression;
using System.Text.Json;

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
/// Verifies <see cref="MapImportExecutor"/> reconstructs a map from an exported zip: it inserts missing
/// biomes/geographies (with base values re-scaled), creates a new index with a unique name, rebuilds tiles
/// and start positions, and enqueues a preview-render job.
/// </summary>
internal sealed class MapImportExecutorTests : SqlServerTestBase
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

    [Test]
    public async Task ExecuteAsync_RoundTrip_RecreatesMapUnderAUniqueNameAsync()
    {
        // Arrange: seed a source map and export it to a real zip via the exporter.
        int sourceIndexId = await SeedSourceMapAsync();
        string zipPath = await ExportToZipAsync(sourceIndexId);
        int processingId = await SeedImportProcessingAsync(zipPath);

        // Act
        await RunImportAsync(processingId);

        // Assert
        await using AscendantEmpireContext verify = await DbFactory.CreateDbContextAsync();

        Processing persisted = await verify.Processing.AsNoTracking().SingleAsync(p => p.ProcessingId == processingId);
        Assert.That(persisted.Status, Is.EqualTo(ProcessingStatus.Complete));
        Assert.That(persisted.ResultReference, Is.Not.Null.And.Not.Empty);

        int newIndexId = int.Parse(persisted.ResultReference!);
        Assert.That(newIndexId, Is.Not.EqualTo(sourceIndexId), "Import should create a brand-new index.");

        CampaignMapGalleryIndex newIndex = await verify.CampaignMapGalleryIndex.AsNoTracking().SingleAsync(x => x.Id == newIndexId);

        using (Assert.EnterMultipleScope())
        {
            // The source map "Import Source" still exists, so the imported copy is auto-renamed.
            Assert.That(newIndex.MapName, Is.EqualTo("Import Source (2)"));
            Assert.That(newIndex.Width, Is.EqualTo(2));
            Assert.That(newIndex.Height, Is.EqualTo(2));
            Assert.That(newIndex.CreationStatus, Is.EqualTo(MapCreationStatus.Complete));
            Assert.That(newIndex.CampaignMapOffsetId, Is.GreaterThan(0), "Offset should point at the first allocated tile id.");
        }

        List<CampaignMapGallery> tiles = await verify.CampaignMapGallery.AsNoTracking()
            .Where(t => t.CampaignMapGalleryIndexId == newIndexId)
            .OrderBy(t => t.YCoord).ThenBy(t => t.XCoord)
            .ToListAsync();

        using (Assert.EnterMultipleScope())
        {
            Assert.That(tiles, Has.Count.EqualTo(4));
            // Coordinates are reconstructed from each tile's row-major RelativeId.
            Assert.That(tiles.Select(t => (t.XCoord, t.YCoord)),
                Is.EqualTo(new[] { (0, 0), (1, 0), (0, 1), (1, 1) }));
            // Each tile gets a distinct, globally-unique CampaignMapGalleryId.
            Assert.That(tiles.Select(t => t.CampaignMapGalleryId).Distinct().Count(), Is.EqualTo(4));
            Assert.That(newIndex.CampaignMapOffsetId, Is.EqualTo(tiles.Min(t => t.CampaignMapGalleryId)));
        }

        List<CampaignMapStartPosition> starts = await verify.CampaignMapStartPositions.AsNoTracking()
            .Where(s => s.CampaignMapGalleryIndexId == newIndexId)
            .OrderBy(s => s.YCoord).ThenBy(s => s.XCoord)
            .ToListAsync();

        Assert.That(starts.Select(s => (s.XCoord, s.YCoord, s.DisplayName, s.LocationScore)),
            Is.EqualTo(new[]
            {
                (0, 0, "North", 30 as int?),
                (1, 1, "South", 10 as int?)
            }));

        // A preview-render job should have been enqueued for the new map.
        bool renderQueued = await verify.Processing.AsNoTracking()
            .AnyAsync(p => p.ProcessType == ProcessType.RenderCampaignMapPreview
                && p.MetadataJson != null
                && p.MetadataJson.Contains(newIndexId.ToString()));
        Assert.That(renderQueued, Is.True, "Import should enqueue a preview-render job for the new map.");
    }

    [Test]
    public async Task ExecuteAsync_InsertsMissingBiomeWithScaledBaseValuesAsync()
    {
        // Arrange: a hand-crafted export referencing a biome/geography absent from the (empty) database.
        MapExportDocument document = BuildDocument(baseForageWholeValue: 50);
        string zipPath = Path.Combine(_tempDirectory, "handcrafted.zip");
        await WriteExportZipAsync(document, zipPath);
        int processingId = await SeedImportProcessingAsync(zipPath);

        // Act
        await RunImportAsync(processingId);

        // Assert
        await using AscendantEmpireContext verify = await DbFactory.CreateDbContextAsync();

        Processing persisted = await verify.Processing.AsNoTracking().SingleAsync(p => p.ProcessingId == processingId);
        Assert.That(persisted.Status, Is.EqualTo(ProcessingStatus.Complete));

        CampaignBiome biome = await verify.CampaignBiomes.AsNoTracking().SingleAsync(b => b.BiomeTypeId == BiomeType.Grassland);
        // The exported whole value (50) is stored scaled by ScaledAmount.Scale (1,000).
        Assert.That(biome.BaseForageValue, Is.EqualTo(50_000));

        bool geographyInserted = await verify.CampaignGeographies.AsNoTracking()
            .AnyAsync(g => g.CampaignMapGeographyTypeId == CampaignMapGeographyType.Flat);
        Assert.That(geographyInserted, Is.True);

        int newIndexId = int.Parse(persisted.ResultReference!);
        List<CampaignMapGallery> tiles = await verify.CampaignMapGallery.AsNoTracking()
            .Where(t => t.CampaignMapGalleryIndexId == newIndexId)
            .ToListAsync();
        Assert.That(tiles, Has.Count.EqualTo(4));
        // RelativeId 3 in a width-2 map reconstructs to (X=1, Y=1).
        Assert.That(tiles.Any(t => t.XCoord == 1 && t.YCoord == 1), Is.True);
    }

    [Test]
    public async Task ExecuteAsync_LeavesExistingBiomeUntouchedAsync()
    {
        // Arrange: seed a Grassland biome whose forage value differs from the imported one.
        await using (AscendantEmpireContext seed = await DbFactory.CreateDbContextAsync())
        {
            seed.CampaignBiomes.Add(new CampaignBiome
            {
                BiomeTypeId = BiomeType.Grassland,
                Name = "Existing Grassland",
                MapEditorHtmlColor = "#111111",
                MovementCost = 1.0m,
                IsTraversable = true,
                IsPaintableOnCampaignMap = true,
                CanBeElevated = true,
                // Stored scaled: whole 99.
                BaseForageValue = 99_000
            });
            seed.CampaignGeographies.Add(new CampaignGeography
            {
                CampaignMapGeographyTypeId = CampaignMapGeographyType.Flat,
                Name = "Existing Flat",
                RelativeElevation = 0,
                MovementCost = 1.0m,
                MapEditorIcon = null,
                IsPaintableOnCampaignMap = true
            });
            await seed.SaveChangesAsync();
        }

        // The import file carries a different Grassland forage value (whole 50).
        MapExportDocument document = BuildDocument(baseForageWholeValue: 50);
        string zipPath = Path.Combine(_tempDirectory, "handcrafted.zip");
        await WriteExportZipAsync(document, zipPath);
        int processingId = await SeedImportProcessingAsync(zipPath);

        // Act
        await RunImportAsync(processingId);

        // Assert: the existing biome is left exactly as it was (insert-only, never overwrite).
        await using AscendantEmpireContext verify = await DbFactory.CreateDbContextAsync();

        Processing persisted = await verify.Processing.AsNoTracking().SingleAsync(p => p.ProcessingId == processingId);
        Assert.That(persisted.Status, Is.EqualTo(ProcessingStatus.Complete));

        CampaignBiome biome = await verify.CampaignBiomes.AsNoTracking().SingleAsync(b => b.BiomeTypeId == BiomeType.Grassland);
        using (Assert.EnterMultipleScope())
        {
            Assert.That(biome.Name, Is.EqualTo("Existing Grassland"));
            Assert.That(biome.BaseForageValue, Is.EqualTo(99_000), "Existing biome value must not be overwritten by import.");
        }
    }

    // --- Helpers ----------------------------------------------------------------------------------

    /// <summary>Runs the import executor for the given processing record against a fresh tracked context.</summary>
    private async Task RunImportAsync(int processingId)
    {
        await using AscendantEmpireContext runContext = await DbFactory.CreateDbContextAsync();

        Processing processing = await runContext.Processing.SingleAsync(p => p.ProcessingId == processingId);

        var channel = new BackgroundProcessingChannel(Options.Create(new BackgroundProcessingSettings()));
        MapImportExecutor executor = new(runContext, channel, new NullLogger<MapImportExecutor>());

        await executor.ExecuteAsync(processing, CancellationToken.None);
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

        return exportProcessing.ResultReference!;
    }

    /// <summary>Creates a Pending import processing record pointing at the staged zip, returning its id.</summary>
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

    /// <summary>Seeds a complete 2x2 source map (biome, geography, index, tiles, start positions).</summary>
    private async Task<int> SeedSourceMapAsync()
    {
        await using AscendantEmpireContext context = await DbFactory.CreateDbContextAsync();

        context.CampaignBiomes.Add(new CampaignBiome
        {
            BiomeTypeId = BiomeType.Grassland,
            Name = "Grassland",
            MapEditorHtmlColor = "#90ee90",
            MovementCost = 1.0m,
            IsTraversable = true,
            RegenerationRate = 1.1m,
            IsPaintableOnCampaignMap = true,
            CanBeElevated = true,
            BaseForageValue = 50_000
        });

        context.CampaignGeographies.Add(new CampaignGeography
        {
            CampaignMapGeographyTypeId = CampaignMapGeographyType.Flat,
            Name = "Flat",
            RelativeElevation = 0,
            MovementCost = 1.0m,
            MapEditorIcon = null,
            IsPaintableOnCampaignMap = true
        });

        await context.SaveChangesAsync();

        CampaignMapGalleryIndex index = new()
        {
            MapName = "Import Source",
            Description = "Source map for import tests",
            Width = 2,
            Height = 2,
            CreationStatus = MapCreationStatus.Complete,
            ProgressPercent = 100,
            CreationEngine = MapCreationEngine.BlankMap,
            Status = "Complete",
            PropertiesJson = "{}",
            CampaignMapOffsetId = 1,
            HorizontalWrap = false,
            VerticalWrap = false
        };

        context.CampaignMapGalleryIndex.Add(index);
        await context.SaveChangesAsync();

        CampaignBiome grassland = await context.CampaignBiomes.SingleAsync(x => x.BiomeTypeId == BiomeType.Grassland);
        CampaignGeography flat = await context.CampaignGeographies.SingleAsync(x => x.CampaignMapGeographyTypeId == CampaignMapGeographyType.Flat);

        int tileId = 1;
        for (int y = 0; y < 2; y++)
        {
            for (int x = 0; x < 2; x++)
            {
                context.CampaignMapGallery.Add(new CampaignMapGallery
                {
                    CampaignMapGalleryId = tileId++,
                    CampaignMapGalleryIndexId = index.Id,
                    XCoord = x,
                    YCoord = y,
                    BiomeTypeId = BiomeType.Grassland,
                    CampaignMapGeographyTypeId = CampaignMapGeographyType.Flat,
                    Biome = grassland,
                    Geography = flat,
                    Index = index
                });
            }
        }

        context.CampaignMapStartPositions.AddRange(
            new CampaignMapStartPosition
            {
                CampaignMapGalleryIndexId = index.Id,
                XCoord = 0,
                YCoord = 0,
                DisplayName = "North",
                LocationScore = 30
            },
            new CampaignMapStartPosition
            {
                CampaignMapGalleryIndexId = index.Id,
                XCoord = 1,
                YCoord = 1,
                DisplayName = "South",
                LocationScore = 10
            });

        await context.SaveChangesAsync();
        return index.Id;
    }

    /// <summary>Builds a portable 2x2 export document referencing a single Grassland/Flat tile palette.</summary>
    private static MapExportDocument BuildDocument(int baseForageWholeValue)
    {
        return new MapExportDocument
        {
            ExportedAt = DateTime.UtcNow,
            Map = new MapExportIndex
            {
                MapName = "Imported Fresh",
                Description = "hand-crafted",
                Width = 2,
                Height = 2,
                CreationEngine = MapCreationEngine.BlankMap,
                HorizontalWrap = false,
                VerticalWrap = false,
                PropertiesJson = "{}",
                CampaignMapOffsetId = 999 // deliberately bogus: import must reassign this.
            },
            Tiles = [.. Enumerable.Range(0, 4).Select(relativeId => new MapExportTile
            {
                RelativeId = relativeId,
                BiomeTypeId = BiomeType.Grassland,
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
                    RegenerationRate = 1.1m,
                    IsPaintableOnCampaignMap = true,
                    MapEditorHtmlColor = "#90ee90",
                    CanBeElevated = true,
                    BaseForageValue = baseForageWholeValue
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
            StartPositions = []
        };
    }

    /// <summary>Writes the given document as <c>export.json</c> inside a zip at <paramref name="path"/>.</summary>
    private static async Task WriteExportZipAsync(MapExportDocument document, string path)
    {
        string json = JsonSerializer.Serialize(document);

        await using FileStream fileStream = new(path, FileMode.Create, FileAccess.Write, FileShare.None);
        using ZipArchive archive = new(fileStream, ZipArchiveMode.Create, leaveOpen: false);

        ZipArchiveEntry entry = archive.CreateEntry("export.json", CompressionLevel.Optimal);
        await using Stream entryStream = entry.Open();
        await using StreamWriter writer = new(entryStream);
        await writer.WriteAsync(json);
    }
}
