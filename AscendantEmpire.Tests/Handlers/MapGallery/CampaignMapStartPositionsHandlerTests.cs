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

/// <summary>
/// Integration tests for <see cref="PutCampaignMapStartPositionsHandler"/>.
/// </summary>
/// <remarks>
/// The Put handler clears the existing rows with <c>ExecuteDeleteAsync</c> inside an explicit transaction, neither of which the in-memory provider can
/// reproduce, so these stay on the containerised tier. The read-side tests live in <see cref="GetCampaignMapStartPositionsHandlerTests"/>.
/// </remarks>
internal class CampaignMapStartPositionsHandlerTests : SqlServerTestBase
{
    private int _mapId;

    [SetUp]
    public async Task SetupHandlersAsync()
    {
        _mapId = await StartPositionsMapSeed.SeedAsync(DbFactory);
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
}
