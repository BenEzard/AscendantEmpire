using AscendantEmpire.Application.Common;
using AscendantEmpire.Application.Data;
using AscendantEmpire.Application.Features.Maps.MapGallery.Classes;
using AscendantEmpire.Application.Features.Maps.MapGallery.Handlers;
using AscendantEmpire.Domain.Entities.Maps.CampaignMaps;

using Microsoft.Extensions.Logging.Abstractions;

using static AscendantEmpire.Application.Common.ServiceResultBase;

namespace AscendantEmpire.Tests.Handlers.MapGallery;

/// <summary>
/// Integration tests for <see cref="GetCampaignMapStartPositionsHandler"/>.
/// </summary>
/// <remarks>
/// The read path is ordinary EF LINQ, so it sits on the in-memory tier. The write-side tests need a container and live in
/// <see cref="CampaignMapStartPositionsHandlerTests"/>.
/// </remarks>
internal class GetCampaignMapStartPositionsHandlerTests : InMemoryTestBase
{
    private int _mapId;

    [SetUp]
    public async Task SeedMapAsync()
    {
        _mapId = await StartPositionsMapSeed.SeedAsync(DbFactory);
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
    public async Task GetShouldReturnOrderedResultsAsync()
    {
        await using (AscendantEmpireContext seedContext = await DbFactory.CreateDbContextAsync())
        {
            // Added out of order so the assertion below proves the handler, not the insert order, does the sorting.
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
}
