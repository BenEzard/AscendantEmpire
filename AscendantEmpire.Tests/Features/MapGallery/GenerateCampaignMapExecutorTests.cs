using AscendantEmpire.Application.Data;
using AscendantEmpire.Application.Features.Maps.MapGallery.Executors;
using AscendantEmpire.Application.Options;
using AscendantEmpire.Domain.Entities.BackgroundProcessing;
using AscendantEmpire.Domain.Entities.Maps.CampaignMaps;
using AscendantEmpire.Domain.Enums.Maps;
using AscendantEmpire.Domain.Enums.Processing;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace AscendantEmpire.Tests.Features.MapGallery;

/// <summary>
/// Integration tests for <see cref="GenerateCampaignMapExecutor"/> against a real SQL Server (Testcontainers).
/// </summary>
/// <remarks>
/// Covers the blank-map happy path (Processing -> Complete with tiles generated) and the failure path (Processing -> Failed, index -> InError).
/// </remarks>
internal sealed class GenerateCampaignMapExecutorTests : SqlServerTestBase
{
    private ServiceProvider _provider = null!;

    [SetUp]
    public async Task ExecutorSetUp()
    {
        // The blank-map generator opens its own scope via IServiceScopeFactory and resolves a
        // scoped AscendantEmpireContext, so we need a provider pointed at the same test database.
        await using AscendantEmpireContext ctx = await DbFactory.CreateDbContextAsync();
        string connectionString = ctx.Database.GetConnectionString()!;

        var services = new ServiceCollection();
        services.AddDbContext<AscendantEmpireContext>(o => o.UseSqlServer(connectionString));
        _provider = services.BuildServiceProvider();
    }

    [TearDown]
    public async Task ExecutorTearDown()
    {
        await _provider.DisposeAsync();
    }

    [Test, Description("Blank-map generation completes: Processing -> Complete/100%, index -> Complete with all tiles and an assigned offset.")]
    public async Task BlankMapGeneration_Completes_Async()
    {
        const int width = 3;
        const int height = 3;

        // The generator inserts tiles referencing the default biome + Flat geography; both are FK-enforced.
        await SeedBiomeAndGeographyAsync();
        int indexId = await SeedPendingBlankIndexAsync(width, height, defaultBiome: BiomeType.Grassland);
        int processingId = await SeedProcessingAsync(indexId);

        // Act — run the executor with a tracked context (mirrors how the dispatcher calls it).
        await using (AscendantEmpireContext runCtx = await DbFactory.CreateDbContextAsync())
        {
            Processing processing = await runCtx.Processing.SingleAsync(p => p.ProcessingId == processingId);
            GenerateCampaignMapExecutor executor = BuildExecutor(runCtx);
            await executor.ExecuteAsync(processing, CancellationToken.None);
        }

        // Assert — read fresh state.
        await using AscendantEmpireContext verifyCtx = await DbFactory.CreateDbContextAsync();
        Processing persistedProcessing = await verifyCtx.Processing.AsNoTracking().SingleAsync(p => p.ProcessingId == processingId);
        CampaignMapGalleryIndex persistedIndex = await verifyCtx.CampaignMapGalleryIndex.AsNoTracking().SingleAsync(i => i.Id == indexId);
        int tileCount = await verifyCtx.CampaignMapGallery.CountAsync(t => t.CampaignMapGalleryIndexId == indexId);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(persistedProcessing.Status, Is.EqualTo(ProcessingStatus.Complete), "Processing should be Complete.");
            Assert.That(persistedProcessing.ProgressPercent, Is.EqualTo(100), "Processing progress should be 100%.");
            Assert.That(persistedProcessing.CompletedAt, Is.Not.Null, "Processing CompletedAt should be set.");

            Assert.That(persistedIndex.CreationStatus, Is.EqualTo(MapCreationStatus.Complete), "Index should be Complete.");
            Assert.That(persistedIndex.ProgressPercent, Is.EqualTo(100), "Index progress should be 100%.");
            Assert.That(persistedIndex.CampaignMapOffsetId, Is.GreaterThan(0), "Index should have an assigned CampaignMapOffsetId.");
            Assert.That(persistedIndex.ErrorMessage, Is.Null, "Index should have no error on success.");

            Assert.That(tileCount, Is.EqualTo(width * height), "All tiles should have been generated.");
        }
    }

    [Test, Description("An unsupported engine fails cleanly: Processing -> Failed, index -> InError with an error message.")]
    public async Task UnsupportedEngine_MarksFailed_Async()
    {
        // An out-of-range engine value triggers the executor's switch default (throw -> FailAsync).
        int indexId = await SeedPendingBlankIndexAsync(3, 3, defaultBiome: BiomeType.Grassland, engine: (MapCreationEngine)999);
        int processingId = await SeedProcessingAsync(indexId);

        await using (AscendantEmpireContext runCtx = await DbFactory.CreateDbContextAsync())
        {
            Processing processing = await runCtx.Processing.SingleAsync(p => p.ProcessingId == processingId);
            GenerateCampaignMapExecutor executor = BuildExecutor(runCtx);
            await executor.ExecuteAsync(processing, CancellationToken.None);
        }

        await using AscendantEmpireContext verifyCtx = await DbFactory.CreateDbContextAsync();
        Processing persistedProcessing = await verifyCtx.Processing.AsNoTracking().SingleAsync(p => p.ProcessingId == processingId);
        CampaignMapGalleryIndex persistedIndex = await verifyCtx.CampaignMapGalleryIndex.AsNoTracking().SingleAsync(i => i.Id == indexId);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(persistedProcessing.Status, Is.EqualTo(ProcessingStatus.Failed), "Processing should be Failed.");
            Assert.That(persistedProcessing.ErrorMessage, Is.Not.Null.And.Not.Empty, "Processing should carry an error message.");

            Assert.That(persistedIndex.CreationStatus, Is.EqualTo(MapCreationStatus.InError), "Index should be InError.");
            Assert.That(persistedIndex.ErrorMessage, Is.Not.Null.And.Not.Empty, "Index should carry an error message.");
        }
    }

    // ---------------------------------------------------------------------

    private GenerateCampaignMapExecutor BuildExecutor(AscendantEmpireContext context)
    {
        return new GenerateCampaignMapExecutor(
            context,
            _provider.GetRequiredService<IServiceScopeFactory>(),
            Options.Create(new MapGenerationSettings()),
            new NullLogger<GenerateCampaignMapExecutor>());
    }

    private async Task SeedBiomeAndGeographyAsync()
    {
        await using AscendantEmpireContext ctx = await DbFactory.CreateDbContextAsync();

        // GenerateBlankCampaignMap inserts tiles referencing a biome + Flat geography; both are FK-enforced.
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

    private async Task<int> SeedPendingBlankIndexAsync(
        int width,
        int height,
        BiomeType defaultBiome,
        MapCreationEngine engine = MapCreationEngine.BlankMap)
    {
        await using AscendantEmpireContext ctx = await DbFactory.CreateDbContextAsync();

        var index = new CampaignMapGalleryIndex
        {
            MapName = "Blank Gen Test",
            Description = "Executor test",
            Width = width,
            Height = height,
            CreationStatus = MapCreationStatus.Pending,
            CreationEngine = engine,
            Status = "Pending creation (#1 in queue)",
            ErrorMessage = string.Empty,
            PropertiesJson = $"{{\"defaultBiome\":\"{defaultBiome}\"}}",
            CampaignMapOffsetId = -1,
            HorizontalWrap = false,
            VerticalWrap = false
        };
        ctx.CampaignMapGalleryIndex.Add(index);
        await ctx.SaveChangesAsync();
        return index.Id;
    }

    private async Task<int> SeedProcessingAsync(int indexId)
    {
        await using AscendantEmpireContext ctx = await DbFactory.CreateDbContextAsync();

        var processing = new Processing
        {
            ProcessType = ProcessType.GenerateCampaignMap,
            Status = ProcessingStatus.Pending,
            StartedAt = DateTime.UtcNow,
            MetadataJson = $"{{\"campaignMapGalleryIndexId\":{indexId}}}"
        };
        ctx.Processing.Add(processing);
        await ctx.SaveChangesAsync();
        return processing.ProcessingId;
    }
}
