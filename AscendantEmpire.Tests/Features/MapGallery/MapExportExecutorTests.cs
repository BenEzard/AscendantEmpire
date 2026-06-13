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

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace AscendantEmpire.Tests.Features.MapGallery;

internal sealed class MapExportExecutorTests : SqlServerTestBase
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
            RegenerationRate = 1.1m,
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

        Processing processing = new()
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
