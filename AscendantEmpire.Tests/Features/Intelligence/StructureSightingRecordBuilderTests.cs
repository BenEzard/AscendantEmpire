using AscendantEmpire.Application.Features.Intelligence.Classes;
using AscendantEmpire.Application.Features.Maps.CampaignMaps.Classes;
using AscendantEmpire.BackendContracts.CampaignMaps;
using AscendantEmpire.Domain.Entities.Games;
using AscendantEmpire.Domain.Enums;
using AscendantEmpire.Domain.Enums.Intelligence;
using AscendantEmpire.Domain.Enums.Maps;
using AscendantEmpire.Domain.Enums.Structures;

namespace AscendantEmpire.Tests.Features.Intelligence;

/// <summary>
/// Pure unit tests for <see cref="StructureSightingRecordBuilder"/> — no database, because it maps sightings it is handed into rows it does not save.
/// </summary>
[TestFixture]
internal class StructureSightingRecordBuilderTests
{
    private const int _GAME_ID = 1;
    private const int _PLAYER_TURN_ID = 40;
    private const int _ACTING_PLAYER_ID = 1;

    private static readonly StructureTimeline _TIMELINE = new()
    {
        StructureInstanceId = 50,
        StructureId = 7,
        CampaignMapTileId = 14,
        Location = new Coord(4, 1),
        Events = [new StructureEvent { ProbeKey = 1, EventType = StructureHistoryEventType.Founded, OwnerPlayerId = _ACTING_PLAYER_ID, PreviousOwnerPlayerId = null, CompletionPercent = 30, TurnSequence = 3 }]
    };

    private static CoalitionStructureSighting Sighting(
        int[] memberPlayerIds,
        bool foundedInView = false,
        bool completedInView = false,
        bool capturedInView = false,
        bool razedInView = false,
        bool progressedInView = false,
        SightingFidelity fidelity = SightingFidelity.Detail)
    {
        return new CoalitionStructureSighting
        {
            CoalitionId = memberPlayerIds.Min(),
            MemberPlayerIds = memberPlayerIds,
            FoundedInView = foundedInView,
            CompletedInView = completedInView,
            CapturedInView = capturedInView,
            RazedInView = razedInView,
            ProgressedInView = progressedInView,
            FirstTurnSequence = 3,
            LastTurnSequence = 8,
            OwnerPlayerId = _ACTING_PLAYER_ID,
            PreviousOwnerPlayerId = capturedInView ? 2 : null,
            CompletionPercent = 30,
            StillVisible = razedInView == false,
            Fidelity = fidelity,
            FirstWitness = new Witness { ObserverId = 10, RecordType = RecordType.Structure, OwnerPlayerId = 2, Location = new Coord(0, 0), Distance = 2, Fidelity = fidelity }
        };
    }

    [Test, Description("Vision is shared by coalitions but reports are addressed to Players, so one coalition's sighting becomes a structure report for each member, on the Structure's one tile.")]
    public void ShouldRaiseOneStructureReportPerMemberOfTheWatchingCoalition()
    {
        IReadOnlyList<PlayerReport> reports = StructureSightingRecordBuilder.BuildReports(_GAME_ID, _PLAYER_TURN_ID, _TIMELINE, [Sighting([5, 2], foundedInView: true, razedInView: true)]);

        PlayerReport report = reports[0];

        using (Assert.EnterMultipleScope())
        {
            Assert.That(reports.Select(r => r.RecipientPlayerId), Is.EqualTo(new[] { 2, 5 }));
            Assert.That(report.ReportType, Is.EqualTo(PlayerReportType.StructureSighted));
            Assert.That(report.RaisedDuringPlayerTurnId, Is.EqualTo(_PLAYER_TURN_ID));
            Assert.That(report.SubjectStructureInstanceId, Is.EqualTo(50));
            Assert.That(report.SubjectStructureId, Is.EqualTo(7));
            Assert.That(report.SubjectUnitInstanceId, Is.Null, "A report has one subject, never both.");
            Assert.That(report.FirstCampaignMapTileId, Is.EqualTo(14));
            Assert.That(report.LastCampaignMapTileId, Is.EqualTo(14));
            Assert.That(report.PathTiles, Is.Empty);
            Assert.That(report.AppearedInView, Is.True, "Founded, on a Structure report.");
            Assert.That(report.DestroyedInView, Is.True, "Razed, on a Structure report.");
            Assert.That(report.CompletedInView, Is.False);
            Assert.That(report.CapturedInView, Is.False);
            Assert.That(report.ExitDirection, Is.Null, "A Structure never leaves.");
            Assert.That(report.FirstTurnSequence, Is.EqualTo(3));
            Assert.That(report.LastTurnSequence, Is.EqualTo(8));
            Assert.That(report.FirstWitnessId, Is.EqualTo(10));
        }
    }

    [Test, Description("A report stores the real Structure and owners whatever fidelity was earned, because stripping them at write time would leave nothing for a later clear sighting to join.")]
    public void ShouldStoreTheTruthWhateverFidelityWasEarned()
    {
        PlayerReport report = StructureSightingRecordBuilder.BuildReports(_GAME_ID, _PLAYER_TURN_ID, _TIMELINE, [Sighting([2], capturedInView: true, fidelity: SightingFidelity.Movement)]).Single();

        using (Assert.EnterMultipleScope())
        {
            Assert.That(report.Fidelity, Is.EqualTo(SightingFidelity.Movement));
            Assert.That(report.SubjectOwnerPlayerId, Is.EqualTo(_ACTING_PLAYER_ID));
            Assert.That(report.SubjectPreviousOwnerPlayerId, Is.EqualTo(2));
            Assert.That(report.SubjectCompletionPercent, Is.EqualTo(30));
        }
    }

    [Test, Description("A sighting of construction progress alone raises no report, but is still remembered, so the map's marker shows the latest progress without cluttering the feed.")]
    public void ShouldRememberButNotReportASightingOfProgressAlone()
    {
        CoalitionStructureSighting progressOnly = Sighting([2], progressedInView: true);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(StructureSightingRecordBuilder.BuildReports(_GAME_ID, _PLAYER_TURN_ID, _TIMELINE, [progressOnly]), Is.Empty);
            Assert.That(StructureSightingRecordBuilder.BuildKnownStructures(_GAME_ID, _PLAYER_TURN_ID, _TIMELINE, [progressOnly]), Has.Count.EqualTo(1));
        }
    }

    [Test, Description("What a Player remembers of a Structure is how it was at the last event they saw, dated by that event, and a Structure watched being razed is remembered as razed.")]
    public void ShouldRememberTheStructureAsLastSeen()
    {
        PlayerKnownStructure known = StructureSightingRecordBuilder.BuildKnownStructures(_GAME_ID, _PLAYER_TURN_ID, _TIMELINE, [Sighting([2], razedInView: true, fidelity: SightingFidelity.Type)]).Single();

        using (Assert.EnterMultipleScope())
        {
            Assert.That(known.PlayerId, Is.EqualTo(2));
            Assert.That(known.StructureInstanceId, Is.EqualTo(50));
            Assert.That(known.StructureId, Is.EqualTo(7));
            Assert.That(known.OwnerPlayerId, Is.EqualTo(_ACTING_PLAYER_ID));
            Assert.That(known.ObservedCompletionPercent, Is.EqualTo(30));
            Assert.That(known.Status, Is.EqualTo(KnownStructureStatus.Razed));
            Assert.That(known.Fidelity, Is.EqualTo(SightingFidelity.Type));
            Assert.That(known.CampaignMapTileId, Is.EqualTo(14));
            Assert.That(known.LastSeenPlayerTurnId, Is.EqualTo(_PLAYER_TURN_ID));
            Assert.That(known.LastSeenTurnSequence, Is.EqualTo(8));
            Assert.That(known.StillVisible, Is.False);
        }
    }

    [Test, Description("A known Structure is only replaced by a sighting at least as recent, by the same rule as a known Force, so an older Turn never rolls a memory back.")]
    [TestCase(40, 5, true, Description = "The same Turn and sequence is a re-run, which replaces itself.")]
    [TestCase(40, 4, false, Description = "Earlier in the same Turn.")]
    [TestCase(41, 1, true, Description = "Any sequence of a later Turn.")]
    [TestCase(39, 99, false, Description = "Any sequence of an earlier Turn.")]
    public void ShouldLetOnlyAnEqualOrLaterSightingSupersedeAKnownStructure(int candidateTurnId, int candidateSequence, bool expectedToSupersede)
    {
        var existing = new PlayerKnownStructure { LastSeenPlayerTurnId = 40, LastSeenTurnSequence = 5 };
        var candidate = new PlayerKnownStructure { LastSeenPlayerTurnId = candidateTurnId, LastSeenTurnSequence = candidateSequence };

        Assert.That(StructureSightingRecordBuilder.Supersedes(candidate, existing), Is.EqualTo(expectedToSupersede));
    }

    [Test, Description("Copying a sighting onto a memory changes what is known of the Structure but never which row, Game, Player or Structure the memory is.")]
    public void ShouldCopyASightingWithoutChangingTheMemorysIdentity()
    {
        var existing = new PlayerKnownStructure { Id = 3, GameId = _GAME_ID, PlayerId = 2, StructureInstanceId = 50, OwnerPlayerId = 2, ObservedCompletionPercent = 10, Status = KnownStructureStatus.Standing };
        var sighting = new PlayerKnownStructure { Id = 0, GameId = 99, PlayerId = 98, StructureInstanceId = 97, OwnerPlayerId = _ACTING_PLAYER_ID, ObservedCompletionPercent = 70, Status = KnownStructureStatus.Razed };

        StructureSightingRecordBuilder.CopySighting(from: sighting, to: existing);

        using (Assert.EnterMultipleScope())
        {
            Assert.That((existing.Id, existing.GameId, existing.PlayerId, existing.StructureInstanceId), Is.EqualTo((3, _GAME_ID, 2, 50)));
            Assert.That(existing.OwnerPlayerId, Is.EqualTo(_ACTING_PLAYER_ID));
            Assert.That(existing.ObservedCompletionPercent, Is.EqualTo(70));
            Assert.That(existing.Status, Is.EqualTo(KnownStructureStatus.Razed));
        }
    }
}
