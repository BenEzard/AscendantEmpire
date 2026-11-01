using AscendantEmpire.Application.Common;
using AscendantEmpire.Application.Common.Behaviours;
using AscendantEmpire.Application.Common.Messaging;
using AscendantEmpire.Application.Common.Validation;
using AscendantEmpire.Application.Data;
using AscendantEmpire.Application.Features.Maps.MapGallery.Handlers;
using AscendantEmpire.BackendContracts.MapGallery.UpdateCampaignMapRivers;
using AscendantEmpire.Domain.Entities.Maps.CampaignMaps;
using AscendantEmpire.Domain.Enums.Maps;
using AscendantEmpire.FrontendContracts.MapGallery.UpdateCampaignMapRivers;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

using static AscendantEmpire.Application.Common.ServiceResultBase;

namespace AscendantEmpire.Tests.Features.MapGallery;

/// <summary>
/// Integration tests for <see cref="UpdateCampaignMapRiversHandler"/>: setting river edges on a gallery map.
/// </summary>
/// <remarks>
/// <para>
/// In-memory tier. The handler writes through tracked entities and one <c>SaveChanges</c>, reads with ordinary LINQ, and leans on no database
/// constraint, so a container would buy nothing here — see <c>docs/testing.md</c>.
/// </para>
/// <para>
/// Sent through a real mediator rather than straight at the handler, so the validator in the pipeline is exercised on the way in, and so the index and
/// Biome reads the handler delegates resolve as they do in the API.
/// </para>
/// <para>
/// The map is <see cref="RiverMapSeed"/>'s: five wide, four high, sea along row 0. <c>East(0, 1)</c> is the vertical edge running down from the coast at
/// corner (1, 1), so it is the natural first stretch of a river and most of these tests start from it.
/// </para>
/// </remarks>
[TestFixture]
internal class UpdateCampaignMapRiversHandlerTests : InMemoryTestBase
{
    private IMediator _mediator = null!;

    [SetUp]
    public void SetUpMediator()
    {
        // A real mediator over the test's own store, so the request travels the same handler chain the API uses: the validator behaviour first, then the
        // handler, which in turn sends for the gallery index and the Biome catalogue. Both a factory and a scoped context are registered because the map
        // gallery handlers are split between the two styles.
        ServiceProvider provider = new ServiceCollection()
            .AddSingleton(DbFactory)
            .AddScoped(_ => DbFactory.CreateDbContext())
            .AddMediator(typeof(UpdateCampaignMapRiversHandler).Assembly)
            .AddValidatorsFromAssembly(typeof(UpdateCampaignMapRiversHandler).Assembly)
            // AddMediator registers the handlers; the behaviour that runs the validators ahead of them is wired by AddApplicationServices, which pulls in
            // far more than these tests need. Registered by hand so a malformed request is refused here exactly as it is in the API.
            .AddTransient(typeof(IPipelineBehaviour<,>), typeof(ValidationBehaviour<,>))
            .AddLogging()
            .BuildServiceProvider();

        _mediator = provider.GetRequiredService<IMediator>();
    }

    private static RiverEdgeChange Change(int x, int y, RiverEdgeSide edge, RiverWidth width)
    {
        return new RiverEdgeChange { X = x, Y = y, Edge = edge, Width = width };
    }

    private Task<ServiceResult<int>> SendAsync(int mapId, params RiverEdgeChange[] changes)
    {
        return _mediator.Send(new UpdateCampaignMapRiversRequest(mapId, changes), CancellationToken.None);
    }

    /// <summary>The two river columns of one tile, read back from the store.</summary>
    private async Task<(RiverWidth South, RiverWidth East)> ReadEdgesAsync(int mapId, int x, int y)
    {
        await using AscendantEmpireContext context = await DbFactory.CreateDbContextAsync();

        CampaignMapGallery tile = await context.CampaignMapGallery
            .AsNoTracking()
            .SingleAsync(t => t.CampaignMapGalleryIndexId == mapId && t.XCoord == x && t.YCoord == y);

        return (tile.SouthRiver, tile.EastRiver);
    }

    /// <summary>The edges a refusal named, off the error detail the response carries for the editor.</summary>
    private static IReadOnlyCollection<IRiverEdgeUpdateFail> RefusedEdges(ServiceResultBase result)
    {
        return ((RiverEdgeUpdateFailCollection)result.ErrorDetail!).RefusedEdges;
    }

    // ── Writing ───────────────────────────────────────────────────────────────────────────────────────────────────────

    [Test, Description("A river drawn from the coast inland writes its width to the SouthRiver and EastRiver columns of the tiles that own the edges.")]
    public async Task SetsTheOwningTilesColumnsAsync()
    {
        int mapId = await RiverMapSeed.SeedAsync(DbFactory);

        ServiceResult<int> result = await SendAsync(
            mapId,
            Change(0, 1, RiverEdgeSide.East, RiverWidth.Large),
            Change(1, 1, RiverEdgeSide.South, RiverWidth.Medium));

        (RiverWidth South, RiverWidth East) mouthTile = await ReadEdgesAsync(mapId, 0, 1);
        (RiverWidth South, RiverWidth East) inlandTile = await ReadEdgesAsync(mapId, 1, 1);

        Assert.Multiple(() =>
        {
            Assert.That(result.HasSucceeded, Is.True, () => string.Join("; ", result.Errors));
            Assert.That(result.SuccessValue, Is.EqualTo(2));
            Assert.That(mouthTile.East, Is.EqualTo(RiverWidth.Large));
            Assert.That(inlandTile.South, Is.EqualTo(RiverWidth.Medium));
        });
    }

    [Test, Description("Erasing the inland end of a river puts that edge back to None and leaves the rest of the river alone.")]
    public async Task ErasesTheInlandEndAsync()
    {
        int mapId = await RiverMapSeed.SeedAsync(DbFactory);

        await SendAsync(mapId, Change(0, 1, RiverEdgeSide.East, RiverWidth.Medium), Change(1, 1, RiverEdgeSide.South, RiverWidth.Medium));

        ServiceResult<int> result = await SendAsync(mapId, Change(1, 1, RiverEdgeSide.South, RiverWidth.None));

        (RiverWidth South, RiverWidth East) mouthTile = await ReadEdgesAsync(mapId, 0, 1);
        (RiverWidth South, RiverWidth East) inlandTile = await ReadEdgesAsync(mapId, 1, 1);

        Assert.Multiple(() =>
        {
            Assert.That(result.HasSucceeded, Is.True, () => string.Join("; ", result.Errors));
            Assert.That(inlandTile.South, Is.EqualTo(RiverWidth.None));
            Assert.That(mouthTile.East, Is.EqualTo(RiverWidth.Medium));
        });
    }

    [Test, Description("An edge already at the requested width costs no write and is not counted, so a repeated click is a no-op.")]
    public async Task AnUnchangedEdgeIsNotCountedAsync()
    {
        int mapId = await RiverMapSeed.SeedAsync(DbFactory);

        await SendAsync(mapId, Change(0, 1, RiverEdgeSide.East, RiverWidth.Medium));

        ServiceResult<int> result = await SendAsync(mapId, Change(0, 1, RiverEdgeSide.East, RiverWidth.Medium));

        Assert.Multiple(() =>
        {
            Assert.That(result.HasSucceeded, Is.True, () => string.Join("; ", result.Errors));
            Assert.That(result.SuccessValue, Is.Zero);
        });
    }

    // ── Refusals ──────────────────────────────────────────────────────────────────────────────────────────────────────

    [Test, Description("An edge with no river running from it to the coast is refused, so a river cannot be started inland.")]
    public async Task RefusesAnEdgeThatDoesNotReachTheSeaAsync()
    {
        int mapId = await RiverMapSeed.SeedAsync(DbFactory);

        ServiceResult<int> result = await SendAsync(mapId, Change(2, 2, RiverEdgeSide.East, RiverWidth.Medium));

        (RiverWidth South, RiverWidth East) tile = await ReadEdgesAsync(mapId, 2, 2);

        Assert.Multiple(() =>
        {
            Assert.That(result.StatusCode, Is.EqualTo(ServiceResultStatus.GenericError));
            Assert.That(tile.East, Is.EqualTo(RiverWidth.None), "Nothing should have been written.");
            Assert.That(RefusedEdges(result).Single().X, Is.EqualTo(2));
        });
    }

    [Test, Description("Erasing an edge in the middle of a river is refused, and nothing in the batch is written.")]
    public async Task RefusesAnEraseThatWouldOrphanTheRestOfTheRiverAsync()
    {
        int mapId = await RiverMapSeed.SeedAsync(DbFactory);

        await SendAsync(
            mapId,
            Change(0, 1, RiverEdgeSide.East, RiverWidth.Medium),
            Change(1, 1, RiverEdgeSide.South, RiverWidth.Medium),
            Change(1, 2, RiverEdgeSide.East, RiverWidth.Medium));

        ServiceResult<int> result = await SendAsync(mapId, Change(1, 1, RiverEdgeSide.South, RiverWidth.None));

        (RiverWidth South, RiverWidth East) middleTile = await ReadEdgesAsync(mapId, 1, 1);

        Assert.Multiple(() =>
        {
            Assert.That(result.StatusCode, Is.EqualTo(ServiceResultStatus.GenericError));
            Assert.That(middleTile.South, Is.EqualTo(RiverWidth.Medium), "The erase should not have landed.");

            // The edge named is the one left stranded, not the one the author clicked.
            Assert.That(RefusedEdges(result).Single().Edge, Is.EqualTo(RiverEdgeSide.East));
            Assert.That(RefusedEdges(result).Single().Y, Is.EqualTo(2));
        });
    }

    [Test, Description("Widening a stretch of river further inland than a narrower one is refused, and carries the width the edge still holds.")]
    public async Task RefusesARiverThatWidensInlandAsync()
    {
        int mapId = await RiverMapSeed.SeedAsync(DbFactory);

        await SendAsync(mapId, Change(0, 1, RiverEdgeSide.East, RiverWidth.Medium), Change(1, 1, RiverEdgeSide.South, RiverWidth.Medium));

        ServiceResult<int> result = await SendAsync(mapId, Change(1, 1, RiverEdgeSide.South, RiverWidth.Large));

        (RiverWidth South, RiverWidth East) inlandTile = await ReadEdgesAsync(mapId, 1, 1);

        Assert.Multiple(() =>
        {
            Assert.That(result.StatusCode, Is.EqualTo(ServiceResultStatus.GenericError));
            Assert.That(inlandTile.South, Is.EqualTo(RiverWidth.Medium), "The widening should not have landed.");
            Assert.That(RefusedEdges(result).Single().OriginalWidth, Is.EqualTo(RiverWidth.Medium));
        });
    }

    [Test, Description("A large river narrowing to medium as it runs inland is accepted, which is the way round that is allowed.")]
    public async Task AcceptsARiverThatNarrowsInlandAsync()
    {
        int mapId = await RiverMapSeed.SeedAsync(DbFactory);

        ServiceResult<int> result = await SendAsync(
            mapId,
            Change(0, 1, RiverEdgeSide.East, RiverWidth.Large),
            Change(1, 1, RiverEdgeSide.South, RiverWidth.Medium));

        Assert.That(result.HasSucceeded, Is.True, () => string.Join("; ", result.Errors));
    }

    [Test, Description("An edge with sea on both banks is refused, because a river between two stretches of sea means nothing.")]
    public async Task RefusesAnEdgeBetweenTwoWaterTilesAsync()
    {
        int mapId = await RiverMapSeed.SeedAsync(DbFactory);

        // East(0, 0) runs between two tiles of the sea along the top row.
        ServiceResult<int> result = await SendAsync(mapId, Change(0, 0, RiverEdgeSide.East, RiverWidth.Large));

        Assert.Multiple(() =>
        {
            Assert.That(result.StatusCode, Is.EqualTo(ServiceResultStatus.GenericError));
            Assert.That(result.Errors.Single(), Does.Contain("water on both sides"));
        });
    }

    [Test, Description("The last column's East edge faces off a map that does not wrap, and is refused.")]
    public async Task RefusesAnEdgeFacingOffANonWrappingMapAsync()
    {
        int mapId = await RiverMapSeed.SeedAsync(DbFactory);

        ServiceResult<int> result = await SendAsync(mapId, Change(RiverMapSeed.MAP_WIDTH - 1, 1, RiverEdgeSide.East, RiverWidth.Medium));

        Assert.Multiple(() =>
        {
            Assert.That(result.StatusCode, Is.EqualTo(ServiceResultStatus.GenericError));
            Assert.That(result.Errors.Single(), Does.Contain("faces off the map"));
        });
    }

    [Test, Description("The same edge is a real edge shared with column 0 on a map that wraps horizontally, and is accepted.")]
    public async Task AcceptsTheSeamEdgeOnAWrappingMapAsync()
    {
        int mapId = await RiverMapSeed.SeedAsync(DbFactory, horizontalWrap: true);

        ServiceResult<int> result = await SendAsync(mapId, Change(RiverMapSeed.MAP_WIDTH - 1, 1, RiverEdgeSide.East, RiverWidth.Medium));

        (RiverWidth South, RiverWidth East) seamTile = await ReadEdgesAsync(mapId, RiverMapSeed.MAP_WIDTH - 1, 1);

        Assert.Multiple(() =>
        {
            Assert.That(result.HasSucceeded, Is.True, () => string.Join("; ", result.Errors));
            Assert.That(seamTile.East, Is.EqualTo(RiverWidth.Medium));
        });
    }

    [Test, Description("A coordinate past the edge of the map is refused before anything is loaded, naming the coordinate.")]
    public async Task RefusesACoordinateOffTheMapAsync()
    {
        int mapId = await RiverMapSeed.SeedAsync(DbFactory);

        ServiceResult<int> result = await SendAsync(mapId, Change(RiverMapSeed.MAP_WIDTH, 1, RiverEdgeSide.South, RiverWidth.Medium));

        Assert.Multiple(() =>
        {
            Assert.That(result.StatusCode, Is.EqualTo(ServiceResultStatus.GenericError));
            Assert.That(result.Errors.Single(), Does.Contain($"({RiverMapSeed.MAP_WIDTH}, 1)"));
        });
    }

    [Test, Description("A batch with one illegal edge in it lands none of its edges, since the rules judge the network the whole batch would make.")]
    public async Task RefusesTheWholeBatchWhenOneEdgeIsIllegalAsync()
    {
        int mapId = await RiverMapSeed.SeedAsync(DbFactory);

        ServiceResult<int> result = await SendAsync(
            mapId,
            Change(0, 1, RiverEdgeSide.East, RiverWidth.Medium),
            Change(3, 3, RiverEdgeSide.East, RiverWidth.Medium));

        (RiverWidth South, RiverWidth East) legalTile = await ReadEdgesAsync(mapId, 0, 1);

        Assert.Multiple(() =>
        {
            Assert.That(result.StatusCode, Is.EqualTo(ServiceResultStatus.GenericError));
            Assert.That(legalTile.East, Is.EqualTo(RiverWidth.None), "The legal edge should not have landed either.");
        });
    }

    [Test, Description("A request naming a map id no map has answers NotFound, so a stale editor tab is told the map is gone rather than that its edge is wrong.")]
    public async Task RefusesAnUnknownMapAsync()
    {
        int mapId = await RiverMapSeed.SeedAsync(DbFactory);

        // A plausible id rather than a negative one, which the validator would reject on shape before the handler ever looked for the map.
        ServiceResult<int> result = await SendAsync(mapId + 1000, Change(0, 1, RiverEdgeSide.East, RiverWidth.Medium));

        Assert.That(result.StatusCode, Is.EqualTo(ServiceResultStatus.NotFound));
    }

    [Test, Description("A request with no edges in it is refused by the validator, before the handler is reached.")]
    public async Task RefusesAnEmptyRequestAsync()
    {
        int mapId = await RiverMapSeed.SeedAsync(DbFactory);

        ServiceResult<int> result = await SendAsync(mapId);

        Assert.Multiple(() =>
        {
            Assert.That(result.HasSucceeded, Is.False);
            Assert.That(result.Errors, Is.Not.Empty);
        });
    }

    [Test, Description("The same edge listed twice in one request is refused, rather than letting the last one silently win.")]
    public async Task RefusesTheSameEdgeTwiceAsync()
    {
        int mapId = await RiverMapSeed.SeedAsync(DbFactory);

        ServiceResult<int> result = await SendAsync(
            mapId,
            Change(0, 1, RiverEdgeSide.East, RiverWidth.Medium),
            Change(0, 1, RiverEdgeSide.East, RiverWidth.Large));

        Assert.Multiple(() =>
        {
            Assert.That(result.HasSucceeded, Is.False);
            Assert.That(result.Errors.Any(error => error.Contains("more than once")), Is.True, () => string.Join("; ", result.Errors));
        });
    }
}
