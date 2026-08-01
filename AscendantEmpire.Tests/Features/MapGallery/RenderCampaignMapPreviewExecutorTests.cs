using AscendantEmpire.Application.Data;
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
/// Integration tests for <see cref="RenderCampaignMapPreviewExecutor"/>.
/// </summary>
/// <remarks>
/// <para>
/// Covers the happy path: the executor reloads a map's tiles, renders the preview PNGs, writes their paths back
/// onto the index, and marks the Processing record Complete.
/// </para>
/// <para>
/// The executor reads through plain EF queries and saves normally — the work that matters here is the SkiaSharp render — so the in-memory tier suffices.
/// </para>
/// </remarks>
internal sealed class RenderCampaignMapPreviewExecutorTests : InMemoryTestBase
{
    private string _outputDirectory = null!;

    [SetUp]
    public void ExecutorSetUp()
    {
        // Render previews into an isolated temp directory so the test can assert the files exist and clean up.
        _outputDirectory = Path.Combine(Path.GetTempPath(), "AscendantEmpire.Tests", Guid.NewGuid().ToString("N"));
    }

    [TearDown]
    public void ExecutorTearDown()
    {
        if (Directory.Exists(_outputDirectory))
        {
            Directory.Delete(_outputDirectory, recursive: true);
        }
    }

    [Test, Description("Preview render completes: Processing -> Complete/100%, index gets both PNG paths, and the files exist on disk.")]
    public async Task PreviewRender_Completes_Async()
    {
        const int width = 3;
        const int height = 3;

        await SeedBiomeAndGeographyAsync();
        int indexId = await SeedCompleteIndexWithTilesAsync(width, height);
        int processingId = await SeedProcessingAsync(indexId);

        // Act — run the executor with a tracked context (mirrors how the dispatcher calls it).
        await using (AscendantEmpireContext runCtx = await DbFactory.CreateDbContextAsync())
        {
            Processing processing = await runCtx.Processing.SingleAsync(p => p.ProcessingId == processingId);
            RenderCampaignMapPreviewExecutor executor = BuildExecutor(runCtx);
            await executor.ExecuteAsync(processing, CancellationToken.None);
        }

        // Assert — read fresh state.
        await using AscendantEmpireContext verifyCtx = await DbFactory.CreateDbContextAsync();
        Processing persistedProcessing = await verifyCtx.Processing.AsNoTracking().SingleAsync(p => p.ProcessingId == processingId);
        CampaignMapGalleryIndex persistedIndex = await verifyCtx.CampaignMapGalleryIndex.AsNoTracking().SingleAsync(i => i.Id == indexId);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(persistedProcessing.Status, Is.EqualTo(ProcessingStatus.Complete), "Processing should be Complete.");
            Assert.That(persistedProcessing.ProgressPercent, Is.EqualTo(100), "Processing progress should be 100%.");
            Assert.That(persistedProcessing.CompletedAt, Is.Not.Null, "Processing CompletedAt should be set.");

            Assert.That(persistedIndex.GeographyMapPath, Is.Not.Null.And.Not.Empty, "Index should have a geography preview path.");
            Assert.That(persistedIndex.BiomeMapPath, Is.Not.Null.And.Not.Empty, "Index should have a biome preview path.");

            Assert.That(File.Exists(persistedIndex.GeographyMapPath!), Is.True, "Geography preview PNG should exist on disk.");
            Assert.That(File.Exists(persistedIndex.BiomeMapPath!), Is.True, "Biome preview PNG should exist on disk.");
        }
    }

    [Test, Description("A missing index fails cleanly: Processing -> Failed with an error message.")]
    public async Task MissingIndex_MarksFailed_Async()
    {
        // Reference an index id that does not exist so the executor throws and records the failure.
        int processingId = await SeedProcessingAsync(indexId: 999_999);

        await using (AscendantEmpireContext runCtx = await DbFactory.CreateDbContextAsync())
        {
            Processing processing = await runCtx.Processing.SingleAsync(p => p.ProcessingId == processingId);
            RenderCampaignMapPreviewExecutor executor = BuildExecutor(runCtx);
            await executor.ExecuteAsync(processing, CancellationToken.None);
        }

        await using AscendantEmpireContext verifyCtx = await DbFactory.CreateDbContextAsync();
        Processing persistedProcessing = await verifyCtx.Processing.AsNoTracking().SingleAsync(p => p.ProcessingId == processingId);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(persistedProcessing.Status, Is.EqualTo(ProcessingStatus.Failed), "Processing should be Failed.");
            Assert.That(persistedProcessing.ErrorMessage, Is.Not.Null.And.Not.Empty, "Processing should carry an error message.");
        }
    }

    // ---------------------------------------------------------------------

    private RenderCampaignMapPreviewExecutor BuildExecutor(AscendantEmpireContext context)
    {
        return new RenderCampaignMapPreviewExecutor(
            context,
            Options.Create(new MapGenerationSettings { OutputDirectory = _outputDirectory }),
            new NullLogger<RenderCampaignMapPreviewExecutor>());
    }

    private async Task SeedBiomeAndGeographyAsync()
    {
        await using AscendantEmpireContext ctx = await DbFactory.CreateDbContextAsync();

        // Tiles reference a biome + Flat geography; both are FK-enforced.
        ctx.CampaignBiomes.Add(new CampaignBiome
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

        ctx.CampaignGeographies.Add(new CampaignGeography
        {
            CampaignMapGeographyTypeId = CampaignMapGeographyType.Flat,
            Name = "Flat",
            RelativeElevation = 0,
            MovementCost = 1.0m,
            MapEditorIcon = null,
            IsPaintableOnCampaignMap = true
        });

        await ctx.SaveChangesAsync();
    }

    private async Task<int> SeedCompleteIndexWithTilesAsync(int width, int height)
    {
        await using AscendantEmpireContext ctx = await DbFactory.CreateDbContextAsync();

        var index = new CampaignMapGalleryIndex
        {
            MapName = "Preview Render Test",
            Description = "Executor test",
            Width = width,
            Height = height,
            CreationStatus = MapCreationStatus.Complete,
            CreationEngine = MapCreationEngine.PerlinNoiseV1,
            Status = "Creation complete",
            ErrorMessage = null,
            PropertiesJson = "{}",
            CampaignMapOffsetId = 0,
            HorizontalWrap = false,
            VerticalWrap = false
        };
        ctx.CampaignMapGalleryIndex.Add(index);
        await ctx.SaveChangesAsync();

        CampaignBiome biome = ctx.CampaignBiomes.First(b => b.BiomeTypeId == BiomeType.Grassland);
        CampaignGeography geo = ctx.CampaignGeographies.First(g => g.CampaignMapGeographyTypeId == CampaignMapGeographyType.Flat);

        int tileId = 1;
        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                ctx.CampaignMapGallery.Add(new CampaignMapGallery
                {
                    CampaignMapGalleryId = tileId++,
                    CampaignMapGalleryIndexId = index.Id,
                    XCoord = x,
                    YCoord = y,
                    BiomeTypeId = BiomeType.Grassland,
                    CampaignMapGeographyTypeId = CampaignMapGeographyType.Flat,
                    Biome = biome,
                    Geography = geo,
                    Index = index
                });
            }
        }

        await ctx.SaveChangesAsync();
        return index.Id;
    }

    private async Task<int> SeedProcessingAsync(int indexId)
    {
        await using AscendantEmpireContext ctx = await DbFactory.CreateDbContextAsync();

        var processing = new Domain.Entities.BackgroundProcessing.Processing
        {
            ProcessType = ProcessType.RenderCampaignMapPreview,
            Status = ProcessingStatus.Pending,
            StartedAt = DateTime.UtcNow,
            MetadataJson = $"{{\"campaignMapGalleryIndexId\":{indexId}}}"
        };
        ctx.Processing.Add(processing);
        await ctx.SaveChangesAsync();
        return processing.ProcessingId;
    }
}
