using AscendantEmpire.Application.Data;
using AscendantEmpire.Application.Features.Maps.MapGallery.Executors;
using AscendantEmpire.Application.Features.Maps.MapResources.Classes;
using AscendantEmpire.Application.Features.Maps.MapResources.Services;
using AscendantEmpire.Domain.Entities.BackgroundProcessing;
using AscendantEmpire.Domain.Entities.Maps.CampaignMaps;
using AscendantEmpire.Domain.Enums.Maps;
using AscendantEmpire.Domain.Enums.Processing;
using AscendantEmpire.Domain.Enums.System;
using AscendantEmpire.Tests.Features.Maps.MapResources;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;

using static AscendantEmpire.Application.Common.ServiceResultBase;

namespace AscendantEmpire.Tests.Features.MapGallery;

/// <summary>
/// Integration tests for <see cref="GenerateCampaignMapExecutor"/> against a real SQL Server (Testcontainers).
/// </summary>
/// <remarks>
/// Covers the blank-map happy path (Processing -> Complete with tiles generated), the failure path (Processing -> Failed, index -> InError), and the pass that
/// places the MapResources the generated tiles' Biomes imply - including that a map still completes when that pass cannot run.
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

    // --- Biome-implied MapResources --------------------------------------

    [Test, Description("Generating a wooded map gives every one of its tiles the Timber its Biome implies, with nothing authored on it, so each game can roll its own scenario.")]
    public async Task ShouldPlaceTimberOnEveryWoodedTileWhenABlankForestMapIsGeneratedAsync()
    {
        const int width = 3;
        const int height = 3;

        int timberTypeId = await SeedTimberAsync(BiomeType.Forest);
        int indexId = await SeedPendingBlankIndexAsync(width, height, defaultBiome: BiomeType.Forest);
        int processingId = await SeedProcessingAsync(indexId);

        await RunExecutorAsync(processingId);

        await using AscendantEmpireContext verifyCtx = await DbFactory.CreateDbContextAsync();
        CampaignMapGalleryIndex persistedIndex = await verifyCtx.CampaignMapGalleryIndex.AsNoTracking().SingleAsync(i => i.Id == indexId);
        List<CampaignMapGalleryResource> placements = await ReadPlacementsAsync(verifyCtx, indexId);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(persistedIndex.CreationStatus, Is.EqualTo(MapCreationStatus.Complete), "The map should still complete.");
            Assert.That(placements, Has.Count.EqualTo(width * height), "Every Forest tile should hold Timber.");
            Assert.That(placements.Select(p => p.MapResourceTypeId), Is.All.EqualTo(timberTypeId), "Every placement should be the Timber type.");
            Assert.That(placements.Select(p => p.Slot), Is.All.EqualTo(1), "An empty tile's first free slot is slot 1.");
            Assert.That(placements.Select(p => p.InitialQuantity), Is.All.Null, "Generation sets no quantity: a Distribution starts at its scenario's amounts.");
            Assert.That(placements.Select(p => p.MapResourceDistributionScenarioId), Is.All.Null, "The scenario is rolled per game, not pinned at generation.");
        }
    }

    [Test, Description("A map of a Biome with no Timber habitat gains no placements, so the pass leaves unwooded maps exactly as they were generated.")]
    public async Task ShouldPlaceNoTimberWhenNoTileIsWoodedAsync()
    {
        await SeedBiomeAndGeographyAsync();
        await SeedTimberAsync(BiomeType.Forest);
        int indexId = await SeedPendingBlankIndexAsync(3, 3, defaultBiome: BiomeType.Grassland);
        int processingId = await SeedProcessingAsync(indexId);

        await RunExecutorAsync(processingId);

        await using AscendantEmpireContext verifyCtx = await DbFactory.CreateDbContextAsync();
        CampaignMapGalleryIndex persistedIndex = await verifyCtx.CampaignMapGalleryIndex.AsNoTracking().SingleAsync(i => i.Id == indexId);
        List<CampaignMapGalleryResource> placements = await ReadPlacementsAsync(verifyCtx, indexId);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(persistedIndex.CreationStatus, Is.EqualTo(MapCreationStatus.Complete), "The map should complete.");
            Assert.That(placements, Is.Empty, "Grassland has no Timber habitat, so no tile should gain any.");
        }
    }

    [Test, Description("A database that has not seeded Timber still generates maps: the MapResources a Biome implies are catalogue data a map does not depend on.")]
    public async Task ShouldCompleteGenerationWhenTimberIsNotSeededAsync()
    {
        await SeedForestBiomeAndFlatGeographyAsync();
        int indexId = await SeedPendingBlankIndexAsync(3, 3, defaultBiome: BiomeType.Forest);
        int processingId = await SeedProcessingAsync(indexId);

        await RunExecutorAsync(processingId);

        await using AscendantEmpireContext verifyCtx = await DbFactory.CreateDbContextAsync();
        CampaignMapGalleryIndex persistedIndex = await verifyCtx.CampaignMapGalleryIndex.AsNoTracking().SingleAsync(i => i.Id == indexId);
        List<CampaignMapGalleryResource> placements = await ReadPlacementsAsync(verifyCtx, indexId);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(persistedIndex.CreationStatus, Is.EqualTo(MapCreationStatus.Complete), "A map with no Timber to place is bare, not broken.");
            Assert.That(persistedIndex.ErrorMessage, Is.Null, "Nothing went wrong, so no error should be recorded.");
            Assert.That(placements, Is.Empty, "There is no Timber type to place.");
        }
    }

    [Test, Description("A failing placement pass leaves the map Complete, because its tiles are already written and an author can place what is missing by hand.")]
    public async Task ShouldCompleteGenerationWhenThePlacementPassFailsAsync()
    {
        await SeedBiomeAndGeographyAsync();
        int indexId = await SeedPendingBlankIndexAsync(3, 3, defaultBiome: BiomeType.Grassland);
        int processingId = await SeedProcessingAsync(indexId);

        await RunExecutorAsync(processingId, new StubBiomeImpliedMapResourceService(throwInstead: false));

        await AssertMapCompletedAsync(indexId, "a placement pass that returns a failure");
    }

    [Test, Description("A throwing placement pass leaves the map Complete too, so an unexpected fault there cannot put a fully generated map into error.")]
    public async Task ShouldCompleteGenerationWhenThePlacementPassThrowsAsync()
    {
        await SeedBiomeAndGeographyAsync();
        int indexId = await SeedPendingBlankIndexAsync(3, 3, defaultBiome: BiomeType.Grassland);
        int processingId = await SeedProcessingAsync(indexId);

        await RunExecutorAsync(processingId, new StubBiomeImpliedMapResourceService(throwInstead: true));

        await AssertMapCompletedAsync(indexId, "a placement pass that throws");
    }

    // ---------------------------------------------------------------------

    /// <summary>
    /// Stands in for the placement pass, so the executor's "never fail the map for this" rule can be pinned.
    /// </summary>
    private sealed class StubBiomeImpliedMapResourceService(bool throwInstead) : IBiomeImpliedMapResourceService
    {
        public Task<ServiceResult<int>> PlaceForGeneratedMapAsync(int campaignMapGalleryIndexId, CancellationToken cancellationToken)
        {
            if (throwInstead)
            {
                throw new InvalidOperationException("Placing the Biome-implied MapResources went wrong.");
            }

            return Task.FromResult(ServiceResult<int>.Fail("Placing the Biome-implied MapResources went wrong."));
        }

        public Task<ServiceResult<BiomeImpliedResourcePlan>> PlanPaintAsync(
            IReadOnlyCollection<CampaignMapGallery> paintedTiles,
            BiomeType? paintedBiome,
            CampaignMapGeographyType? paintedGeography,
            CancellationToken cancellationToken)
        {
            return Task.FromResult(ServiceResult<BiomeImpliedResourcePlan>.Succeed(BiomeImpliedResourcePlan.None));
        }

        public Task ApplyPaintPlanAsync(BiomeImpliedResourcePlan plan, CancellationToken cancellationToken)
        {
            return Task.CompletedTask;
        }
    }

    /// <summary>
    /// Runs the executor over one job the way the background dispatcher does, with a tracked context.
    /// </summary>
    private async Task RunExecutorAsync(int processingId, IBiomeImpliedMapResourceService? impliedMapResources = null)
    {
        await using AscendantEmpireContext runCtx = await DbFactory.CreateDbContextAsync();
        Processing processing = await runCtx.Processing.SingleAsync(p => p.ProcessingId == processingId);
        GenerateCampaignMapExecutor executor = BuildExecutor(runCtx, impliedMapResources);
        await executor.ExecuteAsync(processing, CancellationToken.None);
    }

    /// <summary>
    /// Asserts the map finished cleanly in spite of whatever the placement pass did.
    /// </summary>
    private async Task AssertMapCompletedAsync(int indexId, string despite)
    {
        await using AscendantEmpireContext verifyCtx = await DbFactory.CreateDbContextAsync();
        CampaignMapGalleryIndex persistedIndex = await verifyCtx.CampaignMapGalleryIndex.AsNoTracking().SingleAsync(i => i.Id == indexId);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(persistedIndex.CreationStatus, Is.EqualTo(MapCreationStatus.Complete), $"The map should complete despite {despite}.");
            Assert.That(persistedIndex.ErrorMessage, Is.Null, $"The map should carry no error after {despite}.");
        }
    }

    /// <summary>
    /// Reads the placements on one map's tiles.
    /// </summary>
    private static async Task<List<CampaignMapGalleryResource>> ReadPlacementsAsync(AscendantEmpireContext context, int indexId)
    {
        return await context.CampaignMapGalleryResources
            .AsNoTracking()
            .Where(placement => context.CampaignMapGallery
                .Any(tile => tile.CampaignMapGalleryId == placement.CampaignMapGalleryId && tile.CampaignMapGalleryIndexId == indexId))
            .ToListAsync();
    }

    /// <summary>
    /// Adds the Timber MapResourceType with a Biome-wide habitat for each Biome named, plus the Flat geography the tile insert needs.
    /// </summary>
    private async Task<int> SeedTimberAsync(params BiomeType[] biomes)
    {
        await using AscendantEmpireContext ctx = await DbFactory.CreateDbContextAsync();

        await MapResourceTestSeed.EnsureGeographyAsync(ctx, CampaignMapGeographyType.Flat);

        return await MapResourceTestSeed.SeedTimberAsync(ctx, biomes);
    }

    /// <summary>
    /// Adds the Forest Biome and Flat geography the generated tiles reference, both of which are FK-enforced, without seeding any MapResourceType.
    /// </summary>
    private async Task SeedForestBiomeAndFlatGeographyAsync()
    {
        await using AscendantEmpireContext ctx = await DbFactory.CreateDbContextAsync();

        await MapResourceTestSeed.EnsureBiomeAsync(ctx, BiomeType.Forest);
        await MapResourceTestSeed.EnsureGeographyAsync(ctx, CampaignMapGeographyType.Flat);
    }

    /// <summary>
    /// Builds the executor over one context, with the real placement pass unless a stub is given.
    /// </summary>
    /// <param name="impliedMapResources">
    /// A stand-in for the pass that places the MapResources a tile's Biome implies, so the tests that pin "the map completes anyway" can make it fail.
    /// </param>
    private GenerateCampaignMapExecutor BuildExecutor(AscendantEmpireContext context, IBiomeImpliedMapResourceService? impliedMapResources = null)
    {
        return new GenerateCampaignMapExecutor(
            context,
            _provider.GetRequiredService<IServiceScopeFactory>(),
            impliedMapResources ?? new BiomeImpliedMapResourceService(context, new NullLogger<BiomeImpliedMapResourceService>()),
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
            RegenerationPercent = 0.1m,
            IsPaintableOnCampaignMap = true,
            CanBeElevated = true
        });

        ctx.CampaignGeographies.Add(new CampaignGeography
        {
            CampaignMapGeographyTypeId = CampaignMapGeographyType.Flat,
            Name = "Flat",
            RelativeElevation = 0,
            MovementCost = 1.0m,
            IsWater = false,
            IsTraversable = true,
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

        var processing = new Domain.Entities.BackgroundProcessing.Processing
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
