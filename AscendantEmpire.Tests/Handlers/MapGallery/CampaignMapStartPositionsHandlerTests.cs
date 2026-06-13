using AscendantEmpire.Application.Common;
using AscendantEmpire.Application.Data;
using AscendantEmpire.Application.Features.Maps.MapGallery.Classes;
using AscendantEmpire.Application.Features.Maps.MapGallery.Handlers;
using AscendantEmpire.Domain.Entities.Maps.CampaignMaps;
using AscendantEmpire.Domain.Enums.Maps;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;

using static AscendantEmpire.Application.Common.ServiceResultBase;

namespace AscendantEmpire.Tests.Handlers.MapGallery;

internal class CampaignMapStartPositionsHandlerTests : SqlServerTestBase
{
    private int _mapId;

    [SetUp]
    public async Task SetupHandlersAsync()
    {
        await SeedMapAsync();

        await using AscendantEmpireContext context = await DbFactory.CreateDbContextAsync();
        _mapId = await context.CampaignMapGalleryIndex.Select(x => x.Id).SingleAsync();
    }

    [Test]
    public async Task GetShouldReturnNotFoundForMissingMapAsync()
    {
        await using AscendantEmpireContext context = await DbFactory.CreateDbContextAsync();
        GetCampaignMapStartPositionsHandler handler = new(new NullLogger<GetCampaignMapStartPositionsHandler>(), context);

        ServiceResult<IReadOnlyCollection<DTCampaignMapStartPosition>> result =
            await handler.Handle(new GetCampaignMapStartPositionsRequest(-1), CancellationToken.None);

        Assert.That(result.StatusCode, Is.EqualTo(ServiceResultStatus.NotFound));
    }

    [Test]
    public async Task PutShouldRejectDuplicateCoordinatesAsync()
    {
        await using AscendantEmpireContext context = await DbFactory.CreateDbContextAsync();
        PutCampaignMapStartPositionsHandler handler = new(new NullLogger<PutCampaignMapStartPositionsHandler>(), context);

        ServiceResult<IReadOnlyCollection<DTCampaignMapStartPosition>> result =
            await handler.Handle(new PutCampaignMapStartPositionsRequest(_mapId,
            [
                new PutCampaignMapStartPositionItem(0, 0, "A", 1),
                new PutCampaignMapStartPositionItem(0, 0, "B", 2)
            ]), CancellationToken.None);

        Assert.That(result.HasSucceeded, Is.False);
        Assert.That(result.Errors.Single(), Does.Contain("Duplicate start position coordinates"));
    }

    [Test]
    public async Task PutShouldRejectOutOfBoundsCoordinatesAsync()
    {
        await using AscendantEmpireContext context = await DbFactory.CreateDbContextAsync();
        PutCampaignMapStartPositionsHandler handler = new(new NullLogger<PutCampaignMapStartPositionsHandler>(), context);

        ServiceResult<IReadOnlyCollection<DTCampaignMapStartPosition>> result =
            await handler.Handle(new PutCampaignMapStartPositionsRequest(_mapId,
            [
                new PutCampaignMapStartPositionItem(99, 0, "A", 1)
            ]), CancellationToken.None);

        Assert.That(result.HasSucceeded, Is.False);
        Assert.That(result.Errors.Single(), Does.Contain("outside the map bounds"));
    }

    [Test]
    public async Task PutShouldRejectWaterBiomeAsync()
    {
        await using (AscendantEmpireContext seedContext = await DbFactory.CreateDbContextAsync())
        {
            CampaignMapGallery tile = await seedContext.CampaignMapGallery
                .SingleAsync(x => x.CampaignMapGalleryIndexId == _mapId && x.XCoord == 1 && x.YCoord == 0);
            tile.BiomeTypeId = BiomeType.Ocean;
            await seedContext.SaveChangesAsync();
        }

        await using AscendantEmpireContext runContext = await DbFactory.CreateDbContextAsync();
        PutCampaignMapStartPositionsHandler handler = new(new NullLogger<PutCampaignMapStartPositionsHandler>(), runContext);

        ServiceResult<IReadOnlyCollection<DTCampaignMapStartPosition>> result =
            await handler.Handle(new PutCampaignMapStartPositionsRequest(_mapId,
            [
                new PutCampaignMapStartPositionItem(1, 0, "Sea", 1)
            ]), CancellationToken.None);

        Assert.That(result.HasSucceeded, Is.False);
        Assert.That(result.Errors.Single(), Does.Contain("non-water traversable biome"));
    }

    [Test]
    public async Task PutShouldReplaceAllExistingRowsAsync()
    {
        await using (AscendantEmpireContext seedContext = await DbFactory.CreateDbContextAsync())
        {
            seedContext.CampaignMapStartPositions.Add(new CampaignMapStartPosition
            {
                CampaignMapGalleryIndexId = _mapId,
                XCoord = 0,
                YCoord = 0,
                DisplayName = "Old",
                LocationScore = 1
            });
            await seedContext.SaveChangesAsync();
        }

        await using AscendantEmpireContext runContext = await DbFactory.CreateDbContextAsync();
        PutCampaignMapStartPositionsHandler handler = new(new NullLogger<PutCampaignMapStartPositionsHandler>(), runContext);

        ServiceResult<IReadOnlyCollection<DTCampaignMapStartPosition>> result =
            await handler.Handle(new PutCampaignMapStartPositionsRequest(_mapId,
            [
                new PutCampaignMapStartPositionItem(1, 1, "New A", 10),
                new PutCampaignMapStartPositionItem(0, 1, "New B", 20)
            ]), CancellationToken.None);

        Assert.That(result.HasSucceeded, Is.True);
        Assert.That(result.SuccessValue, Has.Count.EqualTo(2));

        await using AscendantEmpireContext verifyContext = await DbFactory.CreateDbContextAsync();
        List<CampaignMapStartPosition> saved = await verifyContext.CampaignMapStartPositions
            .Where(x => x.CampaignMapGalleryIndexId == _mapId)
            .OrderBy(x => x.YCoord)
            .ThenBy(x => x.XCoord)
            .ToListAsync();

        using (Assert.EnterMultipleScope())
        {
            Assert.That(saved, Has.Count.EqualTo(2));
            Assert.That(saved.Select(x => (x.XCoord, x.YCoord)), Is.EqualTo(new[] { (0, 1), (1, 1) }));
            Assert.That(saved.Select(x => x.DisplayName), Is.EqualTo(new[] { "New B", "New A" }));
        }
    }

    [Test]
    public async Task GetShouldReturnOrderedResultsAsync()
    {
        await using (AscendantEmpireContext seedContext = await DbFactory.CreateDbContextAsync())
        {
            seedContext.CampaignMapStartPositions.AddRange(
                new CampaignMapStartPosition
                {
                    CampaignMapGalleryIndexId = _mapId,
                    XCoord = 1,
                    YCoord = 1,
                    DisplayName = "B"
                },
                new CampaignMapStartPosition
                {
                    CampaignMapGalleryIndexId = _mapId,
                    XCoord = 0,
                    YCoord = 0,
                    DisplayName = "A"
                });
            await seedContext.SaveChangesAsync();
        }

        await using AscendantEmpireContext runContext = await DbFactory.CreateDbContextAsync();
        GetCampaignMapStartPositionsHandler handler = new(new NullLogger<GetCampaignMapStartPositionsHandler>(), runContext);

        ServiceResult<IReadOnlyCollection<DTCampaignMapStartPosition>> result =
            await handler.Handle(new GetCampaignMapStartPositionsRequest(_mapId), CancellationToken.None);

        Assert.That(result.HasSucceeded, Is.True);
        Assert.That(result.SuccessValue!.Select(x => (x.XCoord, x.YCoord)), Is.EqualTo(new[] { (0, 0), (1, 1) }));
    }

    private async Task SeedMapAsync()
    {
        await using AscendantEmpireContext context = await DbFactory.CreateDbContextAsync();

        context.CampaignBiomes.AddRange(
            new CampaignBiome
            {
                BiomeTypeId = BiomeType.Grassland,
                Name = "Grassland",
                MovementCost = 1.0m,
                DoesBlockSight = false,
                IsWater = false,
                IsTraversable = true,
                RegenerationRate = 1.0m,
                IsPaintableOnCampaignMap = true,
                MapEditorHtmlColor = "#90ee90",
                CanBeElevated = true
            },
            new CampaignBiome
            {
                BiomeTypeId = BiomeType.Ocean,
                Name = "Ocean",
                MovementCost = 1.0m,
                DoesBlockSight = false,
                IsWater = true,
                IsTraversable = false,
                RegenerationRate = 1.0m,
                IsPaintableOnCampaignMap = true,
                MapEditorHtmlColor = "#0000ff",
                CanBeElevated = false
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

        CampaignMapGalleryIndex map = new()
        {
            MapName = "Test Map",
            Description = "Test",
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

        context.CampaignMapGalleryIndex.Add(map);
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
                    CampaignMapGalleryIndexId = map.Id,
                    XCoord = x,
                    YCoord = y,
                    BiomeTypeId = BiomeType.Grassland,
                    CampaignMapGeographyTypeId = CampaignMapGeographyType.Flat,
                    Biome = grassland,
                    Geography = flat,
                    Index = map
                });
            }
        }

        await context.SaveChangesAsync();
    }
}
