using AscendantEmpire.Application.Common;
using AscendantEmpire.Application.Data;
using AscendantEmpire.Application.Features.Maps.CampaignMaps.Classes;
using AscendantEmpire.Application.Features.Maps.CampaignMaps.Services;
using AscendantEmpire.BackendContracts.CampaignMaps;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;

using NSubstitute;

using static AscendantEmpire.Application.Common.ServiceResultBase;

namespace AscendantEmpire.Tests.Features.Maps.CampaignMaps;

/// <summary>
/// Tests for <see cref="WitnessService"/>: the inversion — searching only near the probed tiles — and who is allowed to be a witness at all.
/// </summary>
/// <remarks>
/// <para>
/// No database. The reader is a <see cref="FakeSightDataReader"/>, the coalitions are substituted, and the field-of-view service is the real one over the
/// same fake, so a witness here is decided by the real sight rules. The context passed in has no provider and is never touched.
/// </para>
/// <para>
/// Player 1 is the acting Player. The map is 10 x 8 Flat Grassland, with its world-map ids starting at 100.
/// </para>
/// </remarks>
[TestFixture]
internal class WitnessServiceTests
{
    private const int _GAME_ID = 1;
    private const int _PLAYER_TURN_ID = 40;
    private const int _ACTING_PLAYER_ID = 1;
    private const int _WATCHING_PLAYER_ID = 2;

    private SightTerrainGrid _grid = null!;
    private FakeSightDataReader _reader = null!;
    private ICoalitionResolver _coalitionResolver = null!;
    private WitnessService _service = null!;
    private AscendantEmpireContext _unusedContext = null!;

    [SetUp]
    public void SetUpService()
    {
        _grid = new SightTerrainGrid(new CampaignMapGeometry(CampaignMapOffsetId: 100, Width: 10, Height: 8, WrapsHorizontally: false, WrapsVertically: false));
        _reader = new FakeSightDataReader(_grid);

        // Three Players, each seeing alone, unless a test arranges an alliance.
        _coalitionResolver = Substitute.For<ICoalitionResolver>();
        GivenCoalitions(SoloCoalitions.From([_ACTING_PLAYER_ID, _WATCHING_PLAYER_ID, 3]));

        IDbContextFactory<AscendantEmpireContext> dbFactory = Substitute.For<IDbContextFactory<AscendantEmpireContext>>();
        var fieldOfViewService = new FieldOfViewService(NullLogger<FieldOfViewService>.Instance, dbFactory, _reader, SightTestData.AllStrategies);

        _service = new WitnessService(NullLogger<WitnessService>.Instance, dbFactory, _reader, _coalitionResolver, fieldOfViewService);
        _unusedContext = new AscendantEmpireContext(new DbContextOptionsBuilder<AscendantEmpireContext>().Options);
    }

    [TearDown]
    public void TearDownContext()
    {
        _unusedContext.Dispose();
    }

    private void GivenCoalitions(IReadOnlyCollection<VisionCoalition> coalitions)
    {
        _coalitionResolver.ResolveAsync(Arg.Any<AscendantEmpireContext>(), _GAME_ID, Arg.Any<CancellationToken>()).Returns(coalitions);
    }

    private static WitnessProbe Probe(long key, int x, int y, int turnSequence)
    {
        return new WitnessProbe { Key = key, Location = new Coord(x, y), TurnSequence = turnSequence };
    }

    private Task<ServiceResult<IReadOnlyDictionary<long, IReadOnlyCollection<WitnessingCoalition>>>> FindAsync(params WitnessProbe[] probes)
    {
        return _service.FindWitnessesAsync(_unusedContext, new WitnessQuery
        {
            GameId = _GAME_ID,
            PlayerTurnId = _PLAYER_TURN_ID,
            ExcludedPlayerIds = [_ACTING_PLAYER_ID],
            Probes = probes
        });
    }

    [Test, Description("Observers are only searched for within the furthest anyone could see of a probed tile, which is what keeps the cost off the map's size.")]
    public async Task ShouldSearchOnlyTheSquareOfTheFurthestSightRoundTheProbedTileAsync()
    {
        _reader.SightReach = 2;

        await FindAsync(Probe(1, 5, 4, 3));

        IEnumerable<int> expectedTileIds = SightRadius.GetSquare(new Coord(5, 4), 2, _grid.Geometry).Select(coord => _grid.IdOf(coord.X, coord.Y));

        Assert.That(_reader.RequestedObserverTileIds, Is.EquivalentTo(expectedTileIds));
    }

    [Test, Description("An excluded Player takes their whole coalition out of the search, since an ally sharing their vision saw it on the same screen.")]
    public async Task ShouldLeaveOutTheWholeCoalitionOfAnExcludedPlayerAsync()
    {
        GivenCoalitions([new VisionCoalition(_ACTING_PLAYER_ID, [_ACTING_PLAYER_ID, 3]), new VisionCoalition(_WATCHING_PLAYER_ID, [_WATCHING_PLAYER_ID])]);

        await FindAsync(Probe(1, 5, 4, 3));

        Assert.That(_reader.RequestedObservingPlayerIds, Is.EquivalentTo(new[] { _WATCHING_PLAYER_ID }));
    }

    [Test, Description("With nobody left who may witness, every probe still gets its empty answer and nothing is read to find that out.")]
    public async Task ShouldAnswerEveryProbeWithoutReadingWhenNobodyIsLeftToWatchAsync()
    {
        GivenCoalitions([new VisionCoalition(_ACTING_PLAYER_ID, [_ACTING_PLAYER_ID])]);

        ServiceResult<IReadOnlyDictionary<long, IReadOnlyCollection<WitnessingCoalition>>> result = await FindAsync(Probe(1, 5, 4, 3), Probe(2, 6, 4, 4));

        using (Assert.EnterMultipleScope())
        {
            Assert.That(result.HasSucceeded, Is.True, () => string.Join("; ", result.Errors));
            Assert.That(result.SuccessValue!.Keys, Is.EquivalentTo(new long[] { 1, 2 }));
            Assert.That(result.SuccessValue.Values.All(answer => answer.Count == 0), Is.True);
            Assert.That(_reader.ReadCount, Is.Zero);
        }
    }

    [Test, Description("Two probes sharing a key are refused before anything is read, since one of their answers could never be found.")]
    public async Task ShouldFailWithoutReadingWhenTwoProbesShareAKeyAsync()
    {
        ServiceResult<IReadOnlyDictionary<long, IReadOnlyCollection<WitnessingCoalition>>> result = await FindAsync(Probe(7, 5, 4, 3), Probe(7, 6, 4, 4));

        using (Assert.EnterMultipleScope())
        {
            Assert.That(result.HasSucceeded, Is.False);
            Assert.That(_reader.ReadCount, Is.Zero);
            await _coalitionResolver.DidNotReceive().ResolveAsync(Arg.Any<AscendantEmpireContext>(), Arg.Any<int>(), Arg.Any<CancellationToken>());
        }
    }

    [Test, Description("A Game that does not exist is reported as not found rather than as a Turn in which nobody saw anything.")]
    public async Task ShouldReturnNotFoundWhenTheGameDoesNotExistAsync()
    {
        _reader.Geometry = null;

        ServiceResult<IReadOnlyDictionary<long, IReadOnlyCollection<WitnessingCoalition>>> result = await FindAsync(Probe(1, 5, 4, 3));

        Assert.That(result.StatusCode, Is.EqualTo(ServiceResultStatus.NotFound));
    }

    [Test, Description("A watchtower destroyed later in the Turn still witnesses what passed it earlier, and nothing from its destruction on.")]
    public async Task ShouldReportAnObserverDestroyedLaterInTheTurnForWhatItSawBeforeAsync()
    {
        _reader.SightReach = 2;
        _reader.Observers.Add(SightTestData.Unit(10, _WATCHING_PLAYER_ID, 5, 4, 2, destroyedAtSequence: 9));

        ServiceResult<IReadOnlyDictionary<long, IReadOnlyCollection<WitnessingCoalition>>> result = await FindAsync(Probe(1, 6, 4, 3), Probe(2, 6, 4, 9));

        Assert.That(result.HasSucceeded, Is.True, () => string.Join("; ", result.Errors));

        using (Assert.EnterMultipleScope())
        {
            Assert.That(result.SuccessValue![1].Single().Witnesses.Single().ObserverId, Is.EqualTo(10), "At sequence 3 the tower was standing and saw the tile.");
            Assert.That(result.SuccessValue[2], Is.Empty, "At sequence 9 it was already gone.");
        }
    }

    [Test, Description("A watchtower razed or captured later in the Turn still witnesses, as a Structure, what passed it before it was lost, and nothing from then on.")]
    public async Task ShouldReportAStructureLostLaterInTheTurnForWhatItSawBeforeAsync()
    {
        _reader.SightReach = 2;
        _reader.Observers.Add(SightTestData.Structure(10, _WATCHING_PLAYER_ID, 5, 4, 2, destroyedAtSequence: 9));

        ServiceResult<IReadOnlyDictionary<long, IReadOnlyCollection<WitnessingCoalition>>> result = await FindAsync(Probe(1, 6, 4, 3), Probe(2, 6, 4, 9));

        Assert.That(result.HasSucceeded, Is.True, () => string.Join("; ", result.Errors));

        using (Assert.EnterMultipleScope())
        {
            Witness witness = result.SuccessValue![1].Single().Witnesses.Single();

            Assert.That(witness.ObserverId, Is.EqualTo(10), "At sequence 3 the tower still stood and saw the tile.");
            Assert.That(witness.RecordType, Is.EqualTo(Domain.Enums.RecordType.Structure));
            Assert.That(result.SuccessValue[2], Is.Empty, "At sequence 9 it had been lost.");
        }
    }

    [Test, Description("The acting Player's own Observers are never searched for, so they can never witness their own Turn.")]
    public async Task ShouldNeverReportTheActingPlayerAsAWitnessAsync()
    {
        _reader.SightReach = 2;
        _reader.Observers.Add(SightTestData.Unit(10, _ACTING_PLAYER_ID, 5, 4, 2));

        ServiceResult<IReadOnlyDictionary<long, IReadOnlyCollection<WitnessingCoalition>>> result = await FindAsync(Probe(1, 6, 4, 3));

        using (Assert.EnterMultipleScope())
        {
            Assert.That(result.HasSucceeded, Is.True, () => string.Join("; ", result.Errors));
            Assert.That(result.SuccessValue![1], Is.Empty);
            Assert.That(_reader.RequestedObservingPlayerIds, Does.Not.Contain(_ACTING_PLAYER_ID));
        }
    }
}
