using AscendantEmpire.Application.Common;
using AscendantEmpire.Application.Data;
using AscendantEmpire.Application.Features.Maps.CampaignMaps.Classes;
using AscendantEmpire.Application.Features.Maps.CampaignMaps.Services;
using AscendantEmpire.Domain.Enums.Maps;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;

using NSubstitute;

namespace AscendantEmpire.Tests.Features.Maps.CampaignMaps;

/// <summary>
/// Tests for <see cref="FieldOfViewService"/>: that it reads the right terrain, in the right order, and hands it to the sight rules.
/// </summary>
/// <remarks>
/// <para>
/// No database. The reader is a <see cref="FakeSightDataReader"/> over a grid, and the context passed in is constructed with no provider at all: it exists
/// only to be handed to the reader, which never touches it. What the rules then do with the terrain is <see cref="FieldOfViewTests"/>' business.
/// </para>
/// <para>
/// The map is 10 x 8 Flat Grassland.
/// </para>
/// </remarks>
[TestFixture]
internal class FieldOfViewServiceTests
{
    private const int _GAME_ID = 1;

    private SightTerrainGrid _grid = null!;
    private FakeSightDataReader _reader = null!;
    private FieldOfViewService _service = null!;
    private AscendantEmpireContext _unusedContext = null!;

    [SetUp]
    public void SetUpService()
    {
        _grid = new SightTerrainGrid(new CampaignMapGeometry(CampaignMapOffsetId: 100, Width: 10, Height: 8, WrapsHorizontally: false, WrapsVertically: false));
        _reader = new FakeSightDataReader(_grid);
        _service = new FieldOfViewService(NullLogger<FieldOfViewService>.Instance, Substitute.For<IDbContextFactory<AscendantEmpireContext>>(), _reader, SightTestData.AllStrategies);
        _unusedContext = new AscendantEmpireContext(new DbContextOptionsBuilder<AscendantEmpireContext>().Options);
    }

    [TearDown]
    public void TearDownContext()
    {
        _unusedContext.Dispose();
    }

    private async Task<IReadOnlyCollection<ObserverFieldOfView>> ViewAsync(params SightObserver[] observers)
    {
        ServiceResultBase.ServiceResult<IReadOnlyCollection<ObserverFieldOfView>> result = await _service.GetFieldOfViewAsync(_unusedContext, _GAME_ID, _grid.Geometry, observers);

        Assert.That(result.HasSucceeded, Is.True, () => string.Join("; ", result.Errors));

        return result.SuccessValue!;
    }

    [Test, Description("The occupied tiles are read on their own first, because their terrain decides how far each Observer sees and so what else to read.")]
    public async Task ShouldReadTheOccupiedTilesBeforeTheGroundAroundThemAsync()
    {
        IReadOnlyCollection<ObserverFieldOfView> views = await ViewAsync(SightTestData.Unit(1, 2, 5, 4, 1));

        using (Assert.EnterMultipleScope())
        {
            Assert.That(_reader.TerrainRequests, Has.Count.EqualTo(2));
            Assert.That(_reader.TerrainRequests[0], Is.EquivalentTo(new[] { _grid.IdOf(5, 4) }), "Pass one is the occupied tile alone.");
            Assert.That(_reader.TerrainRequests[1], Has.Count.EqualTo(8), "Pass two is the ring in range, without re-reading the occupied tile.");
            Assert.That(views.Single().SeenTiles, Has.Count.EqualTo(9));
        }
    }

    [Test, Description("An Observer with a negative Sight is not an Observer, so it gets no view and costs no terrain read.")]
    public async Task ShouldReadNoTerrainForAnObserverWithNegativeSightAsync()
    {
        IReadOnlyCollection<ObserverFieldOfView> views = await ViewAsync(SightTestData.Unit(1, 2, 5, 4, -1));

        using (Assert.EnterMultipleScope())
        {
            Assert.That(views, Is.Empty);
            Assert.That(_reader.TerrainRequests, Is.Empty);
        }
    }

    [Test, Description("An Observer standing somewhere with no terrain costs only its own view, never the rest of the Player's map.")]
    public async Task ShouldLeaveOutAnObserverWhoseTileCannotBeReadAsync()
    {
        IReadOnlyCollection<ObserverFieldOfView> views = await ViewAsync(SightTestData.Unit(1, 2, 50, 50, 1), SightTestData.Unit(2, 2, 5, 4, 1));

        Assert.That(views.Select(view => view.Observer.Id), Is.EqualTo(new[] { 2 }));
    }

    [Test, Description("A blinded Observer's second pass reads only its ring of neighbours, not the square its Sight would otherwise reach.")]
    public async Task ShouldReadOnlyTheRingAroundABlindedObserverAsync()
    {
        _grid.Paint(5, 4, BiomeType.Jungle);

        await ViewAsync(SightTestData.Unit(1, 2, 5, 4, 3));

        Assert.That(_reader.TerrainRequests[1], Has.Count.EqualTo(8));
    }
}
