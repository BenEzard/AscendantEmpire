using AscendantEmpire.Application.Features.Maps.CampaignMaps.Classes;
using AscendantEmpire.BackendContracts.CampaignMaps;
using AscendantEmpire.Domain.Enums.Maps;

namespace AscendantEmpire.Tests.Features.Maps.CampaignMaps;

/// <summary>
/// Pure unit tests for <see cref="WitnessResolution"/> — no database, because it only interprets fields of view it is handed.
/// </summary>
/// <remarks>
/// <para>
/// These pin what makes an Observer a witness: it saw the tile, it belongs to a coalition allowed to witness, and it had not yet been destroyed at the
/// probed moment. The views are computed by the real <see cref="FieldOfView"/> rules, so cover and range behave as they do in play.
/// </para>
/// <para>
/// Player 1 is the acting Player throughout and is never passed as an observing coalition. The map is 10 x 8 Flat Grassland.
/// </para>
/// </remarks>
[TestFixture]
internal class WitnessResolutionTests
{
    private const int _ACTING_PLAYER_ID = 1;
    private const int _WATCHING_PLAYER_ID = 2;
    private const int _PLAYER_TURN_ID = 40;

    private static readonly IReadOnlyCollection<VisionCoalition> _OBSERVING_COALITIONS = SoloCoalitions.From([_WATCHING_PLAYER_ID, 3]);

    private SightTerrainGrid _grid = null!;

    [SetUp]
    public void SetUpGrid()
    {
        _grid = new SightTerrainGrid(new CampaignMapGeometry(CampaignMapOffsetId: 0, Width: 10, Height: 8, WrapsHorizontally: false, WrapsVertically: false));
    }

    private static WitnessProbe Probe(long key, int x, int y, int turnSequence, ForceSubject? force = null)
    {
        return new WitnessProbe { Key = key, Location = new Coord(x, y), TurnSequence = turnSequence, Force = force };
    }

    private IReadOnlyDictionary<long, IReadOnlyCollection<WitnessingCoalition>> Resolve(IReadOnlyCollection<WitnessProbe> probes, params SightObserver[] observers)
    {
        return WitnessResolution.Resolve(
            _PLAYER_TURN_ID,
            probes,
            SightTestData.ViewsOf(_grid, observers),
            _OBSERVING_COALITIONS,
            new Dictionary<int, long>(),
            _grid.Geometry);
    }

    [Test, Description("An Observer that can see the probed tile makes its coalition a witness, with its distance from the tile recorded.")]
    public void ShouldReportTheWatchingCoalitionWhenAnObserverSeesTheProbedTile()
    {
        IReadOnlyDictionary<long, IReadOnlyCollection<WitnessingCoalition>> answers = Resolve([Probe(1, 6, 4, 3)], SightTestData.Unit(10, _WATCHING_PLAYER_ID, 5, 4, 2));

        WitnessingCoalition coalition = answers[1].Single();
        Witness witness = coalition.Witnesses.Single();

        using (Assert.EnterMultipleScope())
        {
            Assert.That(coalition.CoalitionId, Is.EqualTo(_WATCHING_PLAYER_ID));
            Assert.That(coalition.MemberPlayerIds, Is.EqualTo(new[] { _WATCHING_PLAYER_ID }));
            Assert.That(witness.ObserverId, Is.EqualTo(10));
            Assert.That(witness.OwnerPlayerId, Is.EqualTo(_WATCHING_PLAYER_ID));
            Assert.That(witness.Distance, Is.EqualTo(1));
        }
    }

    [Test, Description("A probe nobody could see still gets an answer, an empty one, so the caller never has to tell a miss from a lost result.")]
    public void ShouldAnswerWithNoCoalitionsWhenTheProbedTileIsOutOfSight()
    {
        IReadOnlyDictionary<long, IReadOnlyCollection<WitnessingCoalition>> answers = Resolve([Probe(1, 9, 4, 3)], SightTestData.Unit(10, _WATCHING_PLAYER_ID, 5, 4, 2));

        using (Assert.EnterMultipleScope())
        {
            Assert.That(answers.ContainsKey(1), Is.True);
            Assert.That(answers[1], Is.Empty);
        }
    }

    [Test, Description("A Force passing behind cover is not witnessed, because the witness question is answered by the same sight rules the map uses.")]
    public void ShouldReportNobodyWhenAJungleHidesTheProbedTile()
    {
        _grid.Paint(6, 4, BiomeType.Jungle);

        IReadOnlyDictionary<long, IReadOnlyCollection<WitnessingCoalition>> answers = Resolve([Probe(1, 7, 4, 3)], SightTestData.Unit(10, _WATCHING_PLAYER_ID, 5, 4, 3));

        Assert.That(answers[1], Is.Empty);
    }

    [Test, Description("An Observer destroyed during the Turn witnesses everything before its destruction and nothing from that sequence on.")]
    [TestCase(8, 1, Description = "The moment before it fell, the watchtower saw the cavalry and still files its report.")]
    [TestCase(9, 0, Description = "It did not live to see what happened at the very sequence it was destroyed.")]
    [TestCase(10, 0, Description = "Nor anything afterwards.")]
    public void ShouldCountADestroyedObserverOnlyForSequencesBeforeItFell(int probedSequence, int expectedCoalitions)
    {
        IReadOnlyDictionary<long, IReadOnlyCollection<WitnessingCoalition>> answers = Resolve(
            [Probe(1, 6, 4, probedSequence)],
            SightTestData.Unit(10, _WATCHING_PLAYER_ID, 5, 4, 2, destroyedAtSequence: 9));

        Assert.That(answers[1], Has.Count.EqualTo(expectedCoalitions));
    }

    [Test, Description("Two Observers of one coalition watching the same tile fold into one answer carrying both, nearest first.")]
    public void ShouldFoldObserversOfOneCoalitionIntoOneAnswer()
    {
        IReadOnlyDictionary<long, IReadOnlyCollection<WitnessingCoalition>> answers = Resolve(
            [Probe(1, 6, 4, 3)],
            SightTestData.Unit(11, _WATCHING_PLAYER_ID, 4, 4, 3),
            SightTestData.Unit(10, _WATCHING_PLAYER_ID, 5, 4, 3));

        WitnessingCoalition coalition = answers[1].Single();

        Assert.That(coalition.Witnesses.Select(witness => witness.ObserverId), Is.EqualTo(new[] { 10, 11 }));
    }

    [Test, Description("The acting Player is never a witness to their own Turn, however close their Observers stand.")]
    public void ShouldNeverReportAPlayerOutsideTheObservingCoalitions()
    {
        IReadOnlyDictionary<long, IReadOnlyCollection<WitnessingCoalition>> answers = Resolve([Probe(1, 6, 4, 3)], SightTestData.Unit(10, _ACTING_PLAYER_ID, 5, 4, 2));

        Assert.That(answers[1], Is.Empty);
    }

    [Test, Description("A tile is seen in full, and while the Force curve is stubbed a Force is too, so intelligence discloses nothing live vision does not.")]
    public void ShouldSeeBothTileAndForceInFullDetailWhileTheForceCurveIsStubbed()
    {
        var force = new ForceSubject { UnitInstanceId = 99, UnitTemplateId = 7, HealthyTroops = 20 };

        IReadOnlyDictionary<long, IReadOnlyCollection<WitnessingCoalition>> answers = Resolve(
            [Probe(1, 6, 4, 3), Probe(2, 6, 4, 3, force)],
            SightTestData.Unit(10, _WATCHING_PLAYER_ID, 5, 4, 2));

        using (Assert.EnterMultipleScope())
        {
            Assert.That(answers[1].Single().BestFidelity, Is.EqualTo(SightingFidelity.Detail), "Seeing a tile is always full fidelity.");
            Assert.That(answers[2].Single().BestFidelity, Is.EqualTo(SightingFidelity.Detail), "The Force rule is a stub that grants full detail.");
        }
    }

    [Test, Description("Two probes sharing a key would leave one answer unfindable, so the resolution refuses rather than silently dropping it.")]
    public void ShouldThrowWhenTwoProbesShareAKey()
    {
        Assert.That(() => Resolve([Probe(1, 6, 4, 3), Probe(1, 7, 4, 4)]), Throws.ArgumentException);
    }
}
