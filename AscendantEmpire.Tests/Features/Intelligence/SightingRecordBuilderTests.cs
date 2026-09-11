using AscendantEmpire.Application.Features.Intelligence.Classes;
using AscendantEmpire.Application.Features.Maps.CampaignMaps.Classes;
using AscendantEmpire.BackendContracts.CampaignMaps;
using AscendantEmpire.Domain.Entities.Games;
using AscendantEmpire.Domain.Enums;
using AscendantEmpire.Domain.Enums.Intelligence;
using AscendantEmpire.Domain.Enums.Maps;

namespace AscendantEmpire.Tests.Features.Intelligence;

/// <summary>
/// Pure unit tests for <see cref="SightingRecordBuilder"/> — no database, because it maps tracks it is handed into rows it does not save.
/// </summary>
[TestFixture]
internal class SightingRecordBuilderTests
{
    private const int _GAME_ID = 1;
    private const int _PLAYER_TURN_ID = 40;
    private const int _HEALTHY_TROOPS = 45;

    private static readonly ForcePath _PATH = new()
    {
        UnitInstanceId = 99,
        OwnerPlayerId = 1,
        UnitTemplateId = 3,
        Steps = [new ForceStep { ProbeKey = 1, Kind = ForceStepKind.Created, CampaignMapTileId = 14, Location = new Coord(4, 1), TurnSequence = 1 }]
    };

    private static ForceTrack Track(int ordinal, int[] pathTileIds, int lastSequence, SightingFidelity fidelity = SightingFidelity.Detail, bool destroyedInView = false)
    {
        return new ForceTrack
        {
            TrackOrdinal = ordinal,
            PathTileIds = pathTileIds,
            FirstTurnSequence = lastSequence - 1,
            LastTurnSequence = lastSequence,
            ExitDirection = destroyedInView ? null : MoveDirection.West,
            StillVisible = false,
            AppearedInView = false,
            DestroyedInView = destroyedInView,
            Fidelity = fidelity,
            FirstWitness = new Witness { ObserverId = 10, RecordType = RecordType.Structure, OwnerPlayerId = 2, Location = new Coord(0, 0), Distance = 2, Fidelity = fidelity }
        };
    }

    private static CoalitionForceTracks CoalitionOf(int[] memberPlayerIds, params ForceTrack[] tracks)
    {
        return new CoalitionForceTracks { CoalitionId = memberPlayerIds.Min(), MemberPlayerIds = memberPlayerIds, Tracks = tracks };
    }

    [Test, Description("Vision is shared by coalitions but reports are addressed to Players, so one coalition's track becomes a report for each member.")]
    public void ShouldRaiseOneReportPerMemberOfTheWatchingCoalition()
    {
        IReadOnlyList<PlayerReport> reports = SightingRecordBuilder.BuildReports(_GAME_ID, _PLAYER_TURN_ID, _PATH, [CoalitionOf([5, 2], Track(0, [14, 15], lastSequence: 3))], _HEALTHY_TROOPS);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(reports.Select(report => report.RecipientPlayerId), Is.EqualTo(new[] { 2, 5 }));
            Assert.That(reports, Has.All.Property(nameof(PlayerReport.RaisedDuringPlayerTurnId)).EqualTo(_PLAYER_TURN_ID));
            Assert.That(reports, Has.All.Property(nameof(PlayerReport.ReportType)).EqualTo(PlayerReportType.ForceSighted));
            Assert.That(reports[0].PathTiles.Select(tile => (tile.Ordinal, tile.CampaignMapTileId)), Is.EqualTo(new[] { (0, 14), (1, 15) }));
            Assert.That(reports[0].FirstCampaignMapTileId, Is.EqualTo(14));
            Assert.That(reports[0].LastCampaignMapTileId, Is.EqualTo(15));
            Assert.That(reports[0].ExitDirection, Is.EqualTo(MoveDirection.West));
            Assert.That(reports[0].FirstWitnessId, Is.EqualTo(10));
            Assert.That(reports[0].FirstWitnessRecordType, Is.EqualTo(RecordType.Structure));
        }
    }

    [Test, Description("A report stores the real Force whatever fidelity was earned, because stripping it at write time would leave a later clear sighting nothing to join up with.")]
    public void ShouldStoreTheTruthWhateverFidelityWasEarned()
    {
        PlayerReport report = SightingRecordBuilder.BuildReports(_GAME_ID, _PLAYER_TURN_ID, _PATH, [CoalitionOf([2], Track(0, [14], lastSequence: 1, SightingFidelity.Movement))], _HEALTHY_TROOPS).Single();

        using (Assert.EnterMultipleScope())
        {
            Assert.That(report.Fidelity, Is.EqualTo(SightingFidelity.Movement));
            Assert.That(report.SubjectUnitInstanceId, Is.EqualTo(99));
            Assert.That(report.SubjectOwnerPlayerId, Is.EqualTo(1));
            Assert.That(report.SubjectUnitTemplateId, Is.EqualTo(3));
            Assert.That(report.SubjectHealthyTroops, Is.EqualTo(_HEALTHY_TROOPS));
        }
    }

    [Test, Description("What a Player remembers of a Force is where they last saw it, so the known Force comes from the latest track, not the first.")]
    public void ShouldRememberTheLatestTrackAsTheKnownForce()
    {
        CoalitionForceTracks coalition = CoalitionOf([2], Track(0, [14, 15], lastSequence: 3), Track(1, [18], lastSequence: 8, SightingFidelity.Type));

        PlayerKnownForce knownForce = SightingRecordBuilder.BuildKnownForces(_GAME_ID, _PLAYER_TURN_ID, _PATH, [coalition], _HEALTHY_TROOPS).Single();

        using (Assert.EnterMultipleScope())
        {
            Assert.That(knownForce.PlayerId, Is.EqualTo(2));
            Assert.That(knownForce.UnitInstanceId, Is.EqualTo(99));
            Assert.That(knownForce.LastSeenCampaignMapTileId, Is.EqualTo(18));
            Assert.That(knownForce.LastSeenTurnSequence, Is.EqualTo(8));
            Assert.That(knownForce.LastSeenPlayerTurnId, Is.EqualTo(_PLAYER_TURN_ID));
            Assert.That(knownForce.Fidelity, Is.EqualTo(SightingFidelity.Type));
            Assert.That(knownForce.Status, Is.EqualTo(KnownForceStatus.Sighted));
        }
    }

    [Test, Description("A Force watched being destroyed is remembered as destroyed rather than forgotten, because knowing it died is knowledge.")]
    public void ShouldRememberAForceWatchedBeingDestroyedAsDestroyed()
    {
        CoalitionForceTracks coalition = CoalitionOf([2], Track(0, [14], lastSequence: 4, destroyedInView: true));

        PlayerKnownForce knownForce = SightingRecordBuilder.BuildKnownForces(_GAME_ID, _PLAYER_TURN_ID, _PATH, [coalition], healthyTroops: null).Single();

        using (Assert.EnterMultipleScope())
        {
            Assert.That(knownForce.Status, Is.EqualTo(KnownForceStatus.Destroyed));
            Assert.That(knownForce.HealthyTroops, Is.Null, "A Force gone before the sightings step has no strength to record.");
        }
    }

    [Test, Description("A sighting replaces a memory only when it is at least as recent, so an older Turn never rolls a Player's knowledge back and a re-run of the same Turn replaces itself.")]
    [TestCase(40, 5, true, Description = "The same Turn and sequence is a re-run, and replacing it with itself is what makes the step safe to retry.")]
    [TestCase(40, 6, true, Description = "Later in the same Turn.")]
    [TestCase(40, 4, false, Description = "Earlier in the same Turn.")]
    [TestCase(41, 1, true, Description = "Any sequence of a later Turn.")]
    [TestCase(39, 99, false, Description = "Any sequence of an earlier Turn.")]
    public void ShouldLetOnlyAnEqualOrLaterSightingSupersedeAKnownForce(int candidateTurnId, int candidateSequence, bool expectedToSupersede)
    {
        var existing = new PlayerKnownForce { LastSeenPlayerTurnId = 40, LastSeenTurnSequence = 5 };
        var candidate = new PlayerKnownForce { LastSeenPlayerTurnId = candidateTurnId, LastSeenTurnSequence = candidateSequence };

        Assert.That(SightingRecordBuilder.Supersedes(candidate, existing), Is.EqualTo(expectedToSupersede));
    }
}
