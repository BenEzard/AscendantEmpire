using AscendantEmpire.Application.Common;
using AscendantEmpire.Application.Common.Behaviours;
using AscendantEmpire.Application.Common.Messaging;
using AscendantEmpire.Application.Common.Validation;
using AscendantEmpire.Application.Data;
using AscendantEmpire.Application.Features.Maps.MapGallery.Handlers;
using AscendantEmpire.Application.Features.Maps.MapResources.Classes;
using AscendantEmpire.Application.Features.Maps.MapResources.Services;
using AscendantEmpire.BackendContracts.CampaignMaps;
using AscendantEmpire.BackendContracts.MapGallery.UpdateCampaignMapTiles;
using AscendantEmpire.Domain.Entities.Maps.CampaignMaps;
using AscendantEmpire.Domain.Entities.Maps.MapResources;
using AscendantEmpire.Domain.Enums.Maps;
using AscendantEmpire.Tests.Features.Maps.MapResources;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

using static AscendantEmpire.Application.Common.ServiceResultBase;

namespace AscendantEmpire.Tests.Features.MapGallery;

/// <summary>
/// Integration tests for <see cref="UpdateCampaignMapTilesHandler"/>: the map editor's Biome and Geography brush.
/// </summary>
/// <remarks>
/// <para>
/// Containerised tier. Every write path goes through <c>ExecuteUpdateAsync</c>, the tile and placement writes share an explicit transaction, and the
/// placements are governed by two unique indexes - three separate reasons in-memory will not do. See <c>docs/testing.md</c>.
/// </para>
/// <para>
/// Sent through a real mediator, because the handler delegates the index, Biome and Geography reads to other handlers on the way in.
/// </para>
/// <para>
/// The map is three tiles wide and two high. Its tile ids are assigned by hand, as every writer of <c>CampaignMapGallery</c> does, and the index's
/// <c>CampaignMapOffsetId</c> points at the first of them - which is what makes a coordinate resolve to a tile id at all.
/// </para>
/// </remarks>
[TestFixture]
internal sealed class UpdateCampaignMapTilesHandlerTests : SqlServerTestBase
{
    private const int _MAP_WIDTH = 3;
    private const int _MAP_HEIGHT = 2;

    /// <summary>
    /// The tile id the test map starts at. Any value does, as long as it is not also an index id - which is exactly what one test below arranges.
    /// </summary>
    private const int _TILE_ID_BASE = 5_000;

    private IMediator _mediator = null!;
    private ServiceProvider _provider = null!;
    private int _mapId;
    private int _timberTypeId;

    [SetUp]
    public async Task SetUpHandlerAsync()
    {
        _provider = BuildProvider();
        _mediator = _provider.GetRequiredService<IMediator>();

        await SeedTerrainCatalogueAsync();

        await using AscendantEmpireContext context = await DbFactory.CreateDbContextAsync();
        _timberTypeId = await MapResourceTestSeed.SeedTimberAsync(context, BiomeType.Forest, BiomeType.Jungle, BiomeType.Woodland);

        _mapId = await SeedMapAsync(_TILE_ID_BASE, BiomeType.Grassland, CampaignMapGeographyType.Flat);
    }

    [TearDown]
    public async Task TearDownHandlerAsync()
    {
        await _provider.DisposeAsync();
    }

    // --- The tiles a paint reaches ---------------------------------------

    [Test, Description("A Biome-only paint writes every tile the brush covered, not one: the tiles it may write are identified by their own ids, not the map's.")]
    public async Task ShouldUpdateEveryPaintedTileWhenOnlyTheBiomeChangesAsync()
    {
        ServiceResult<int, IReadOnlyCollection<TileUpdateFail>?> result = await PaintAsync(
            biome: BiomeType.Desert,
            geography: null,
            coordinates: AllCoordinates());

        List<CampaignMapGallery> tiles = await ReadTilesAsync();

        using (Assert.EnterMultipleScope())
        {
            Assert.That(result.HasSucceeded, Is.True, "Nothing refused the paint, so it should report plain success.");
            Assert.That(result.SuccessValue, Is.EqualTo(_MAP_WIDTH * _MAP_HEIGHT), "Every painted tile should be counted.");
            Assert.That(tiles.Select(t => t.BiomeTypeId), Is.All.EqualTo(BiomeType.Desert), "Every painted tile should hold the new Biome.");
        }
    }

    [Test, Description("A Geography-only paint writes its tiles too, where it used to filter on the map's id and so write nothing at all.")]
    public async Task ShouldUpdateEveryPaintedTileWhenOnlyTheGeographyChangesAsync()
    {
        ServiceResult<int, IReadOnlyCollection<TileUpdateFail>?> result = await PaintAsync(
            biome: null,
            geography: CampaignMapGeographyType.Hilly,
            coordinates: AllCoordinates());

        List<CampaignMapGallery> tiles = await ReadTilesAsync();

        using (Assert.EnterMultipleScope())
        {
            Assert.That(result.SuccessValue, Is.EqualTo(_MAP_WIDTH * _MAP_HEIGHT), "Every painted tile should be counted.");
            Assert.That(
                tiles.Select(t => t.CampaignMapGeographyTypeId),
                Is.All.EqualTo(CampaignMapGeographyType.Hilly),
                "Every painted tile should hold the new Geography.");
        }
    }

    [Test, Description("A refusal names each tile it turned down by that tile's own id, so the editor can put back exactly the tiles that did not take.")]
    public async Task ShouldNameEveryRefusedTileWhenTheBiomeCannotBeElevatedAsync()
    {
        // Ice cannot be elevated, so painting it onto mountainous tiles is refused per tile rather than for the whole request.
        await SetGeographyAsync(CampaignMapGeographyType.Mountainous, new Coord(0, 0), new Coord(1, 0));

        ServiceResult<int, IReadOnlyCollection<TileUpdateFail>?> result = await PaintAsync(
            biome: BiomeType.Ice,
            geography: null,
            coordinates: [new Coord(0, 0), new Coord(1, 0)]);

        TileUpdateFailTile[] refused = [.. (result.ErrorValue ?? []).SelectMany(fail => fail.FailedTiles)];

        using (Assert.EnterMultipleScope())
        {
            Assert.That(result.HasPartiallySucceeded, Is.True, "A paint that left tiles behind is a partial success.");
            Assert.That(refused, Has.Length.EqualTo(2), "Both mountainous tiles should be named, where they used to collapse into one.");
            Assert.That(
                refused.Select(tile => tile.DbId),
                Is.EquivalentTo(new[] { TileId(0, 0), TileId(1, 0) }),
                "Each refusal should carry the tile's own id, not the map's.");
            Assert.That(refused.Select(tile => tile.Coord.Y), Is.All.Zero, "Both refused tiles are on row 0.");
        }
    }

    [Test, Description("A paint touches only the map it names, which a tile id that collides with another map's index id would otherwise break.")]
    public async Task ShouldLeaveAnotherMapsTilesAloneWhenPaintingAsync()
    {
        // The second map's tile ids are allocated to straddle the first map's index id. Before the ids were read from the right column, a paint on one map
        // could write a tile of another that happened to be numbered like its index.
        int otherMapId = await SeedMapAsync(_mapId, BiomeType.Forest, CampaignMapGeographyType.Flat);

        await PaintAsync(biome: BiomeType.Desert, geography: null, coordinates: AllCoordinates());

        List<CampaignMapGallery> otherTiles = await ReadTilesAsync(otherMapId);

        Assert.That(
            otherTiles.Select(tile => tile.BiomeTypeId),
            Is.All.EqualTo(BiomeType.Forest),
            "The other map's tiles should be untouched by a paint on this one.");
    }

    // --- The MapResources a Biome implies --------------------------------

    [Test, Description("Painting a tile into a wooded Biome gives it the Timber that Biome implies, so a hand-drawn forest holds wood like a generated one.")]
    public async Task ShouldPlaceTimberWhenATileIsPaintedForestAsync()
    {
        await PaintAsync(biome: BiomeType.Forest, geography: null, coordinates: [new Coord(0, 0)]);

        List<CampaignMapGalleryResource> placements = await ReadPlacementsAsync();

        using (Assert.EnterMultipleScope())
        {
            Assert.That(placements, Has.Count.EqualTo(1), "The painted tile should have gained Timber.");
            Assert.That(placements.Single().CampaignMapGalleryId, Is.EqualTo(TileId(0, 0)), "It should be on the tile that was painted.");
            Assert.That(placements.Single().MapResourceTypeId, Is.EqualTo(_timberTypeId), "It should be the Timber type.");
            Assert.That(placements.Single().Slot, Is.EqualTo(1), "An empty tile's first free slot is slot 1.");
        }
    }

    [Test, Description("A paint giving both Biome and Geography places Timber as well, since that path writes tiles without any per-tile validation.")]
    public async Task ShouldPlaceTimberWhenBiomeAndGeographyArePaintedTogetherAsync()
    {
        await PaintAsync(biome: BiomeType.Forest, geography: CampaignMapGeographyType.Hilly, coordinates: [new Coord(0, 0)]);

        List<CampaignMapGalleryResource> placements = await ReadPlacementsAsync();

        Assert.That(placements, Has.Count.EqualTo(1), "Timber should be placed whichever way the tile's terrain was written.");
    }

    [Test, Description("Painting a wooded tile out of Timber's habitat removes the placement, so no tile keeps wood its Biome cannot hold.")]
    public async Task ShouldRemoveTimberWhenATileIsPaintedOutOfItsHabitatAsync()
    {
        await PaintAsync(biome: BiomeType.Forest, geography: null, coordinates: [new Coord(0, 0)]);

        await PaintAsync(biome: BiomeType.Desert, geography: null, coordinates: [new Coord(0, 0)]);

        List<CampaignMapGalleryResource> placements = await ReadPlacementsAsync();

        Assert.That(placements, Is.Empty, "Desert has no Timber habitat, so the placement should have gone with the repaint.");
    }

    [Test, Description("Painting Ocean implies its Geography and so takes the both-at-once path, which must remove Timber the same as any other repaint.")]
    public async Task ShouldRemoveTimberWhenPaintingOceanForcesItsGeographyAsync()
    {
        await PaintAsync(biome: BiomeType.Forest, geography: null, coordinates: [new Coord(0, 0)]);

        await PaintAsync(biome: BiomeType.Ocean, geography: null, coordinates: [new Coord(0, 0)]);

        List<CampaignMapGallery> tiles = await ReadTilesAsync();
        List<CampaignMapGalleryResource> placements = await ReadPlacementsAsync();

        using (Assert.EnterMultipleScope())
        {
            Assert.That(
                tiles.Single(tile => tile.CampaignMapGalleryId == TileId(0, 0)).CampaignMapGeographyTypeId,
                Is.EqualTo(CampaignMapGeographyType.DeepWater),
                "Ocean forces deep water, which is what routes this paint through the both-at-once path.");

            Assert.That(placements, Is.Empty, "The Timber should have gone with the repaint.");
        }
    }

    [Test, Description("A pinned scenario holds the whole tile back, so the editor's revert of the reported tile matches what the database actually kept.")]
    public async Task ShouldKeepTheTileAndThePlacementWhenTheAuthorPinnedAScenarioAsync()
    {
        await PaintAsync(biome: BiomeType.Forest, geography: null, coordinates: [new Coord(0, 0)]);
        await AuthorThePlacementAsync(pinScenario: true);

        ServiceResult<int, IReadOnlyCollection<TileUpdateFail>?> result = await PaintAsync(
            biome: BiomeType.Desert,
            geography: null,
            coordinates: [new Coord(0, 0)]);

        await AssertTileHeldOutAsync(result);
    }

    [Test, Description("A chosen starting quantity holds a tile back the same way a pinned scenario does, since both are the author's words about that tile.")]
    public async Task ShouldKeepTheTileAndThePlacementWhenTheAuthorSetAQuantityAsync()
    {
        await PaintAsync(biome: BiomeType.Forest, geography: null, coordinates: [new Coord(0, 0)]);
        await AuthorThePlacementAsync(pinScenario: false);

        ServiceResult<int, IReadOnlyCollection<TileUpdateFail>?> result = await PaintAsync(
            biome: BiomeType.Desert,
            geography: null,
            coordinates: [new Coord(0, 0)]);

        await AssertTileHeldOutAsync(result);
    }

    [Test, Description("A tile whose two slots are taken is still painted and is not reported, because only an automatic convenience did not happen on it.")]
    public async Task ShouldPaintTheTileWithoutTimberWhenBothSlotsAreTakenAsync()
    {
        await FillBothSlotsAsync(new Coord(0, 0));

        ServiceResult<int, IReadOnlyCollection<TileUpdateFail>?> result = await PaintAsync(
            biome: BiomeType.Forest,
            geography: null,
            coordinates: [new Coord(0, 0)]);

        List<CampaignMapGallery> tiles = await ReadTilesAsync();
        List<CampaignMapGalleryResource> placements = await ReadPlacementsAsync();

        using (Assert.EnterMultipleScope())
        {
            Assert.That(result.HasSucceeded, Is.True, "A full tile is not a failed paint, so the request should report plain success.");
            Assert.That(
                tiles.Single(tile => tile.CampaignMapGalleryId == TileId(0, 0)).BiomeTypeId,
                Is.EqualTo(BiomeType.Forest),
                "The tile itself should be painted.");

            Assert.That(placements.Any(p => p.MapResourceTypeId == _timberTypeId), Is.False, "There was no slot left for Timber.");
            Assert.That(placements, Has.Count.EqualTo(2), "The two MapResources already on the tile should be untouched.");
        }
    }

    [Test, Description("Painting the same tiles twice leaves one placement, which is what UX_CampaignMapGalleryResource_Tile_Type would otherwise reject.")]
    public async Task ShouldPlaceTimberOnceWhenTheSameTilesArePaintedTwiceAsync()
    {
        await PaintAsync(biome: BiomeType.Forest, geography: null, coordinates: AllCoordinates());
        await PaintAsync(biome: BiomeType.Forest, geography: null, coordinates: AllCoordinates());

        List<CampaignMapGalleryResource> placements = await ReadPlacementsAsync();

        Assert.That(placements, Has.Count.EqualTo(_MAP_WIDTH * _MAP_HEIGHT), "The second paint should have added nothing.");
    }

    [Test, Description("A database with no Timber seeded still paints, because the MapResources a Biome implies are catalogue data the terrain does not need.")]
    public async Task ShouldPaintWithoutTouchingPlacementsWhenTimberIsNotSeededAsync()
    {
        await RemoveTimberAsync();

        ServiceResult<int, IReadOnlyCollection<TileUpdateFail>?> result = await PaintAsync(
            biome: BiomeType.Forest,
            geography: null,
            coordinates: [new Coord(0, 0)]);

        List<CampaignMapGallery> tiles = await ReadTilesAsync();
        List<CampaignMapGalleryResource> placements = await ReadPlacementsAsync();

        using (Assert.EnterMultipleScope())
        {
            Assert.That(result.HasSucceeded, Is.True, "The paint should succeed.");
            Assert.That(
                tiles.Single(tile => tile.CampaignMapGalleryId == TileId(0, 0)).BiomeTypeId,
                Is.EqualTo(BiomeType.Forest),
                "The tile should be painted.");

            Assert.That(placements, Is.Empty, "There is no Timber type to place.");
        }
    }

    [Test, Description("A failing placement write rolls the tile write back with it, so a tile can never be left holding wood its Biome does not imply.")]
    public async Task ShouldLeaveTheTilesUnchangedWhenThePlacementWriteFailsAsync()
    {
        // The tiles go out through ExecuteUpdate, which takes effect at once, and the placements through the change tracker afterwards. Only the shared
        // transaction makes the pair atomic, and this is what pins it.
        ServiceProvider provider = BuildProvider(useThrowingPlacementWrites: true);
        await using (provider)
        {
            IMediator throwingMediator = provider.GetRequiredService<IMediator>();

            ServiceResult<int, IReadOnlyCollection<TileUpdateFail>?> result = await throwingMediator.Send(
                new UpdateCampaignMapTilesRequest(_mapId, AllCoordinates(), BiomeType.Forest, null),
                CancellationToken.None);

            Assert.That(result.HasSucceeded, Is.False, "The paint should report failure.");
        }

        List<CampaignMapGallery> tiles = await ReadTilesAsync();

        Assert.That(
            tiles.Select(tile => tile.BiomeTypeId),
            Is.All.EqualTo(BiomeType.Grassland),
            "Every tile should still hold the Biome it started with.");
    }

    // ---------------------------------------------------------------------

    /// <summary>
    /// Applies a plan by throwing, so the transaction around the tile and placement writes can be pinned.
    /// </summary>
    private sealed class ThrowingPlacementService(AscendantEmpireContext context, IBiomeImpliedMapResourceService inner) : IBiomeImpliedMapResourceService
    {
        public Task<ServiceResult<int>> PlaceForGeneratedMapAsync(int campaignMapGalleryIndexId, CancellationToken cancellationToken)
        {
            return inner.PlaceForGeneratedMapAsync(campaignMapGalleryIndexId, cancellationToken);
        }

        public Task<ServiceResult<BiomeImpliedResourcePlan>> PlanPaintAsync(
            IReadOnlyCollection<CampaignMapGallery> paintedTiles,
            BiomeType? paintedBiome,
            CampaignMapGeographyType? paintedGeography,
            CancellationToken cancellationToken)
        {
            return inner.PlanPaintAsync(paintedTiles, paintedBiome, paintedGeography, cancellationToken);
        }

        public Task ApplyPaintPlanAsync(BiomeImpliedResourcePlan plan, CancellationToken cancellationToken)
        {
            _ = context;
            throw new InvalidOperationException("Writing the Biome-implied placements went wrong.");
        }
    }

    /// <summary>
    /// A real mediator over this test's database, with the validation behaviour and the placement service the handler depends on.
    /// </summary>
    /// <remarks>
    /// One scoped context for the whole provider, so the handler and the placement service share it - which is what lets the placement writes join the
    /// handler's transaction, exactly as they do in the API.
    /// </remarks>
    private ServiceProvider BuildProvider(bool useThrowingPlacementWrites = false)
    {
        IServiceCollection services = new ServiceCollection()
            .AddSingleton(DbFactory)
            .AddScoped(_ => DbFactory.CreateDbContext())
            .AddMediator(typeof(UpdateCampaignMapTilesHandler).Assembly)
            .AddValidatorsFromAssembly(typeof(UpdateCampaignMapTilesHandler).Assembly)
            // The same two behaviours ApplicationStartup registers, in the same order, so a handler that throws is answered here the way it is in the running
            // API rather than tearing the call down. The host environment is what the exception behaviour reads to decide how much of the exception to say.
            .AddTransient(typeof(IPipelineBehaviour<,>), typeof(ExceptionHandlingBehaviour<,>))
            .AddTransient(typeof(IPipelineBehaviour<,>), typeof(ValidationBehaviour<,>))
            .AddSingleton<IHostEnvironment>(new StubHostEnvironment(StubHostEnvironment.PRODUCTION))
            .AddLogging();

        if (useThrowingPlacementWrites)
        {
            services.AddScoped<IBiomeImpliedMapResourceService>(provider => new ThrowingPlacementService(
                provider.GetRequiredService<AscendantEmpireContext>(),
                new BiomeImpliedMapResourceService(
                    provider.GetRequiredService<AscendantEmpireContext>(),
                    provider.GetRequiredService<ILogger<BiomeImpliedMapResourceService>>())));
        }
        else
        {
            services.AddScoped<IBiomeImpliedMapResourceService, BiomeImpliedMapResourceService>();
        }

        return services.BuildServiceProvider();
    }

    private Task<ServiceResult<int, IReadOnlyCollection<TileUpdateFail>?>> PaintAsync(
        BiomeType? biome,
        CampaignMapGeographyType? geography,
        IReadOnlyCollection<Coord> coordinates)
    {
        return _mediator.Send(new UpdateCampaignMapTilesRequest(_mapId, coordinates, biome, geography), CancellationToken.None);
    }

    /// <summary>
    /// Every coordinate of the test map.
    /// </summary>
    private static Coord[] AllCoordinates()
    {
        return [.. Enumerable.Range(0, _MAP_HEIGHT).SelectMany(y => Enumerable.Range(0, _MAP_WIDTH).Select(x => new Coord(x, y)))];
    }

    /// <summary>
    /// The tile id a coordinate resolves to, the same way the handler derives it from the map's offset.
    /// </summary>
    private static int TileId(int x, int y)
    {
        return _TILE_ID_BASE + x + (y * _MAP_WIDTH);
    }

    /// <summary>
    /// Asserts the tile kept both its Biome and its placement, and was reported as having held out.
    /// </summary>
    private async Task AssertTileHeldOutAsync(ServiceResult<int, IReadOnlyCollection<TileUpdateFail>?> result)
    {
        List<CampaignMapGallery> tiles = await ReadTilesAsync();
        List<CampaignMapGalleryResource> placements = await ReadPlacementsAsync();
        TileUpdateFailTile[] heldOut = [.. (result.ErrorValue ?? []).SelectMany(fail => fail.FailedTiles)];

        using (Assert.EnterMultipleScope())
        {
            Assert.That(
                tiles.Single(tile => tile.CampaignMapGalleryId == TileId(0, 0)).BiomeTypeId,
                Is.EqualTo(BiomeType.Forest),
                "The tile should keep its Biome, so the editor's revert of it is correct.");

            Assert.That(placements, Has.Count.EqualTo(1), "The authored placement should be kept.");
            Assert.That(heldOut, Has.Length.EqualTo(1), "The tile should be reported so the author knows why it did not take.");
            Assert.That(heldOut.Single().DbId, Is.EqualTo(TileId(0, 0)), "The report should name the tile that held out.");
            Assert.That(
                heldOut.Single().OriginalBiomeType,
                Is.EqualTo(BiomeType.Forest),
                "The original Biome is also the current one, because the tile was not written.");
        }
    }

    /// <summary>
    /// Puts author intent on the one placement the test map has, either by pinning a scenario or by setting a quantity.
    /// </summary>
    private async Task AuthorThePlacementAsync(bool pinScenario)
    {
        await using AscendantEmpireContext context = await DbFactory.CreateDbContextAsync();

        CampaignMapGalleryResource placement = await context.CampaignMapGalleryResources.SingleAsync();

        if (pinScenario)
        {
            var scenario = new MapResourceDistributionScenario
            {
                MapResourceTypeId = _timberTypeId,
                ScenarioNumber = 1,
                DisplayName = "Mixed oak"
            };

            context.MapResourceDistributionScenarios.Add(scenario);
            await context.SaveChangesAsync();

            placement.MapResourceDistributionScenarioId = scenario.Id;
        }
        else
        {
            placement.InitialQuantity = 10_000_000;
        }

        await context.SaveChangesAsync();
    }

    /// <summary>
    /// Takes both of a tile's slots with MapResources that are nothing to do with its Biome.
    /// </summary>
    private async Task FillBothSlotsAsync(Coord coord)
    {
        await using AscendantEmpireContext context = await DbFactory.CreateDbContextAsync();

        MapResourceTestCatalogue catalogue = await MapResourceTestSeed.SeedCatalogueAsync(context);

        context.CampaignMapGalleryResources.AddRange(
            new CampaignMapGalleryResource { CampaignMapGalleryId = TileId(coord.X, coord.Y), MapResourceTypeId = catalogue.BerryTypeId, Slot = 1 },
            new CampaignMapGalleryResource { CampaignMapGalleryId = TileId(coord.X, coord.Y), MapResourceTypeId = catalogue.CoalTypeId, Slot = 2 });

        await context.SaveChangesAsync();
    }

    /// <summary>
    /// Deletes the Timber MapResourceType, leaving a database that never seeded it.
    /// </summary>
    private async Task RemoveTimberAsync()
    {
        await using AscendantEmpireContext context = await DbFactory.CreateDbContextAsync();

        await context.MapResourceHabitats.Where(habitat => habitat.MapResourceTypeId == _timberTypeId).ExecuteDeleteAsync();
        await context.MapResourceTypes.Where(type => type.Id == _timberTypeId).ExecuteDeleteAsync();
    }

    /// <summary>
    /// Sets one or more tiles' Geography directly, to set up terrain a paint then has to judge itself against.
    /// </summary>
    private async Task SetGeographyAsync(CampaignMapGeographyType geography, params Coord[] coordinates)
    {
        await using AscendantEmpireContext context = await DbFactory.CreateDbContextAsync();

        int[] tileIds = [.. coordinates.Select(coord => TileId(coord.X, coord.Y))];

        await context.CampaignMapGallery
            .Where(tile => tileIds.Contains(tile.CampaignMapGalleryId))
            .ExecuteUpdateAsync(setters => setters.SetProperty(tile => tile.CampaignMapGeographyTypeId, geography));
    }

    private async Task<List<CampaignMapGallery>> ReadTilesAsync(int? mapId = null)
    {
        await using AscendantEmpireContext context = await DbFactory.CreateDbContextAsync();

        return await context.CampaignMapGallery
            .AsNoTracking()
            .Where(tile => tile.CampaignMapGalleryIndexId == (mapId ?? _mapId))
            .ToListAsync();
    }

    private async Task<List<CampaignMapGalleryResource>> ReadPlacementsAsync()
    {
        await using AscendantEmpireContext context = await DbFactory.CreateDbContextAsync();

        return await context.CampaignMapGalleryResources
            .AsNoTracking()
            .Where(placement => context.CampaignMapGallery
                .Any(tile => tile.CampaignMapGalleryId == placement.CampaignMapGalleryId && tile.CampaignMapGalleryIndexId == _mapId))
            .ToListAsync();
    }

    /// <summary>
    /// Adds the Biomes and Geographies the paints below use, with the properties the terrain rules read.
    /// </summary>
    /// <remarks>
    /// Ice is the one Biome here that cannot be elevated, which is what lets a paint be refused per tile rather than for the whole request. Ocean is water, so
    /// painting it forces deep water.
    /// </remarks>
    private async Task SeedTerrainCatalogueAsync()
    {
        await using AscendantEmpireContext context = await DbFactory.CreateDbContextAsync();

        // Forest, Jungle and Woodland are seeded here with the full set of flags rather than left to MapResourceTestSeed's minimal row, which would default
        // them to unpaintable and unelevatable and so refuse half the paints below.
        context.CampaignBiomes.AddRange(
            BuildBiome(BiomeType.Grassland, isWater: false, canBeElevated: true),
            BuildBiome(BiomeType.Desert, isWater: false, canBeElevated: true),
            BuildBiome(BiomeType.Forest, isWater: false, canBeElevated: true),
            BuildBiome(BiomeType.Jungle, isWater: false, canBeElevated: true),
            BuildBiome(BiomeType.Woodland, isWater: false, canBeElevated: true),
            BuildBiome(BiomeType.Ice, isWater: false, canBeElevated: false),
            BuildBiome(BiomeType.Ocean, isWater: true, canBeElevated: false));

        context.CampaignGeographies.AddRange(
            BuildGeography(CampaignMapGeographyType.Flat, relativeElevation: 0),
            BuildGeography(CampaignMapGeographyType.Hilly, relativeElevation: 1),
            BuildGeography(CampaignMapGeographyType.Mountainous, relativeElevation: 2),
            BuildGeography(CampaignMapGeographyType.DeepWater, relativeElevation: 0));

        await context.SaveChangesAsync();
    }

    private static CampaignBiome BuildBiome(BiomeType biome, bool isWater, bool canBeElevated)
    {
        return new CampaignBiome
        {
            BiomeTypeId = biome,
            Name = biome.ToString(),
            MapEditorHtmlColor = "#90ee90",
            MovementCost = 1m,
            DoesBlockSight = false,
            IsWater = isWater,
            IsTraversable = true,
            RegenerationPercent = 0.1m,
            IsPaintableOnCampaignMap = true,
            CanBeElevated = canBeElevated
        };
    }

    private static CampaignGeography BuildGeography(CampaignMapGeographyType geography, int relativeElevation)
    {
        return new CampaignGeography
        {
            CampaignMapGeographyTypeId = geography,
            Name = geography.ToString(),
            RelativeElevation = relativeElevation,
            MovementCost = 1m,
            MapEditorIcon = null,
            IsPaintableOnCampaignMap = true
        };
    }

    /// <summary>
    /// Adds a complete gallery map whose tiles start at <paramref name="tileIdBase"/>, and returns its index id.
    /// </summary>
    /// <remarks>
    /// <c>CampaignMapOffsetId</c> has to equal the first tile's id, because that is what a coordinate is resolved against. The index must be Complete, or the
    /// handler refuses to paint it at all.
    /// </remarks>
    private async Task<int> SeedMapAsync(int tileIdBase, BiomeType biome, CampaignMapGeographyType geography)
    {
        await using AscendantEmpireContext context = await DbFactory.CreateDbContextAsync();

        var index = new CampaignMapGalleryIndex
        {
            MapName = $"Paint Test {tileIdBase}",
            Description = "Brush test",
            Width = _MAP_WIDTH,
            Height = _MAP_HEIGHT,
            CreationStatus = MapCreationStatus.Complete,
            CreationEngine = MapCreationEngine.BlankMap,
            Status = "Creation complete",
            PropertiesJson = "{}",
            CampaignMapOffsetId = tileIdBase,
            HorizontalWrap = false,
            VerticalWrap = false
        };

        context.CampaignMapGalleryIndex.Add(index);
        await context.SaveChangesAsync();

        // The tile's Biome, Geography and Index navigations are required, so the catalogue rows are loaded rather than left to the foreign keys alone.
        CampaignBiome tileBiome = await context.CampaignBiomes.SingleAsync(b => b.BiomeTypeId == biome);
        CampaignGeography tileGeography = await context.CampaignGeographies.SingleAsync(g => g.CampaignMapGeographyTypeId == geography);

        for (int y = 0; y < _MAP_HEIGHT; y++)
        {
            for (int x = 0; x < _MAP_WIDTH; x++)
            {
                context.CampaignMapGallery.Add(new CampaignMapGallery
                {
                    CampaignMapGalleryId = tileIdBase + x + (y * _MAP_WIDTH),
                    CampaignMapGalleryIndexId = index.Id,
                    XCoord = x,
                    YCoord = y,
                    BiomeTypeId = biome,
                    CampaignMapGeographyTypeId = geography,
                    Biome = tileBiome,
                    Geography = tileGeography,
                    Index = index
                });
            }
        }

        await context.SaveChangesAsync();

        return index.Id;
    }
}
