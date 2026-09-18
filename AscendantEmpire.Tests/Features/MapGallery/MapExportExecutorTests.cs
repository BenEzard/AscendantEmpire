using System.IO.Compression;
using System.Text.Json;

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
/// Integration tests for <see cref="MapExportExecutor"/>: it reads a map and serialises it into a zip.
/// </summary>
/// <remarks>
/// The read-and-serialise path uses only plain EF queries and <c>SaveChanges</c> — no raw SQL or transactions — so the in-memory tier suffices.
/// </remarks>
internal sealed class MapExportExecutorTests : InMemoryTestBase
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

    [Test, Description("Start positions are authored data, not regenerable from tiles, so they must survive into export.json.")]
    public async Task ExecuteAsync_ShouldIncludeStartPositionsInExportAsync()
    {
        int indexId = await SeedMapAsync();
        int processingId = await SeedProcessingAsync(indexId);

        await using (AscendantEmpireContext runContext = await DbFactory.CreateDbContextAsync())
        {
            Processing processing = await runContext.Processing.SingleAsync(x => x.ProcessingId == processingId);
            MapExportExecutor executor = new(
                runContext,
                Options.Create(new ExportSettings { TempDirectory = _tempDirectory }),
                new NullLogger<MapExportExecutor>());

            await executor.ExecuteAsync(processing, CancellationToken.None);
        }

        await using AscendantEmpireContext verifyContext = await DbFactory.CreateDbContextAsync();
        Processing persisted = await verifyContext.Processing.AsNoTracking().SingleAsync(x => x.ProcessingId == processingId);

        Assert.That(persisted.Status, Is.EqualTo(ProcessingStatus.Complete));
        Assert.That(persisted.ResultReference, Is.Not.Null.And.Not.Empty);
        Assert.That(File.Exists(persisted.ResultReference!), Is.True);

        using ZipArchive archive = ZipFile.OpenRead(persisted.ResultReference!);
        ZipArchiveEntry exportEntry = archive.GetEntry("export.json")!;
        await using Stream entryStream = exportEntry.Open();
        using StreamReader reader = new(entryStream);
        string json = await reader.ReadToEndAsync();

        MapExportDocument? document = JsonSerializer.Deserialize<MapExportDocument>(json);

        Assert.That(document, Is.Not.Null);
        Assert.That(document!.StartPositions.Select(x => (x.XCoord, x.YCoord, x.DisplayName, x.LocationScore)),
            Is.EqualTo(new[]
            {
                (0, 0, "North", 30 as int?),
                (1, 1, "South", 10 as int?)
            }));
    }

    [Test, Description("Preview images are bundled under stable names, with bytes compared so a geography/biome mix-up would fail.")]
    public async Task ExecuteAsync_ShouldBundlePreviewImagesWhenPresentAsync()
    {
        int indexId = await SeedMapAsync();

        // Write two fake preview PNGs to disk and point the index at them, mimicking a completed preview render.
        string geographyPath = Path.Combine(_tempDirectory, $"{indexId}_geography.png");
        string biomePath = Path.Combine(_tempDirectory, $"{indexId}_biomes.png");
        byte[] geographyBytes = [1, 2, 3, 4];
        byte[] biomeBytes = [5, 6, 7, 8];
        await File.WriteAllBytesAsync(geographyPath, geographyBytes);
        await File.WriteAllBytesAsync(biomePath, biomeBytes);

        await using (AscendantEmpireContext seedContext = await DbFactory.CreateDbContextAsync())
        {
            CampaignMapGalleryIndex index = await seedContext.CampaignMapGalleryIndex.SingleAsync(x => x.Id == indexId);
            index.GeographyMapPath = geographyPath;
            index.BiomeMapPath = biomePath;
            await seedContext.SaveChangesAsync();
        }

        int processingId = await SeedProcessingAsync(indexId);

        await using (AscendantEmpireContext runContext = await DbFactory.CreateDbContextAsync())
        {
            Processing processing = await runContext.Processing.SingleAsync(x => x.ProcessingId == processingId);
            MapExportExecutor executor = new(
                runContext,
                Options.Create(new ExportSettings { TempDirectory = _tempDirectory }),
                new NullLogger<MapExportExecutor>());

            await executor.ExecuteAsync(processing, CancellationToken.None);
        }

        await using AscendantEmpireContext verifyContext = await DbFactory.CreateDbContextAsync();
        Processing persisted = await verifyContext.Processing.AsNoTracking().SingleAsync(x => x.ProcessingId == processingId);

        Assert.That(persisted.Status, Is.EqualTo(ProcessingStatus.Complete));

        using ZipArchive archive = ZipFile.OpenRead(persisted.ResultReference!);

        ZipArchiveEntry? geographyEntry = archive.GetEntry("geography.png");
        ZipArchiveEntry? biomeEntry = archive.GetEntry("biomes.png");

        Assert.That(geographyEntry, Is.Not.Null);
        Assert.That(biomeEntry, Is.Not.Null);

        Assert.That(await ReadEntryBytesAsync(geographyEntry!), Is.EqualTo(geographyBytes));
        Assert.That(await ReadEntryBytesAsync(biomeEntry!), Is.EqualTo(biomeBytes));
    }

    [Test, Description("Previews are optional, so a map exported before any render produces a smaller archive, not a failure.")]
    public async Task ExecuteAsync_ShouldSucceedWithoutPreviewEntriesWhenNoneRenderedAsync()
    {
        int indexId = await SeedMapAsync();
        int processingId = await SeedProcessingAsync(indexId);

        await using (AscendantEmpireContext runContext = await DbFactory.CreateDbContextAsync())
        {
            Processing processing = await runContext.Processing.SingleAsync(x => x.ProcessingId == processingId);
            MapExportExecutor executor = new(
                runContext,
                Options.Create(new ExportSettings { TempDirectory = _tempDirectory }),
                new NullLogger<MapExportExecutor>());

            await executor.ExecuteAsync(processing, CancellationToken.None);
        }

        await using AscendantEmpireContext verifyContext = await DbFactory.CreateDbContextAsync();
        Processing persisted = await verifyContext.Processing.AsNoTracking().SingleAsync(x => x.ProcessingId == processingId);

        Assert.That(persisted.Status, Is.EqualTo(ProcessingStatus.Complete));

        using ZipArchive archive = ZipFile.OpenRead(persisted.ResultReference!);

        // No preview paths on the index → the zip carries just the JSON, no preview entries.
        Assert.That(archive.GetEntry("export.json"), Is.Not.Null);
        Assert.That(archive.GetEntry("geography.png"), Is.Null);
        Assert.That(archive.GetEntry("biomes.png"), Is.Null);
    }

    private static async Task<byte[]> ReadEntryBytesAsync(ZipArchiveEntry entry)
    {
        await using Stream entryStream = entry.Open();
        using MemoryStream buffer = new();
        await entryStream.CopyToAsync(buffer);
        return buffer.ToArray();
    }

    private async Task<int> SeedMapAsync()
    {
        await using AscendantEmpireContext context = await DbFactory.CreateDbContextAsync();

        context.CampaignBiomes.Add(new CampaignBiome
        {
            BiomeTypeId = BiomeType.Grassland,
            Name = "Grassland",
            MapEditorHtmlColor = "#90ee90",
            MovementCost = 1.0m,
            DoesBlockSight = false,
            IsWater = false,
            IsTraversable = true,
            RegenerationPercent = 0.1m,
            IsPaintableOnCampaignMap = true,
            CanBeElevated = true
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
            MapName = "Export Test",
            Description = "Export test map",
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

    private async Task<int> SeedProcessingAsync(int indexId)
    {
        await using AscendantEmpireContext context = await DbFactory.CreateDbContextAsync();

        Domain.Entities.BackgroundProcessing.Processing processing = new()
        {
            ProcessType = ProcessType.ExportGalleryMap,
            Status = ProcessingStatus.Pending,
            StartedAt = DateTime.UtcNow,
            MetadataJson = $"{{\"mapGalleryIndexId\":{indexId}}}"
        };

        context.Processing.Add(processing);
        await context.SaveChangesAsync();
        return processing.ProcessingId;
    }
}
