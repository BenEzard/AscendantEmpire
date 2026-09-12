using AscendantEmpire.Application.Features.Intelligence.Classes;
using AscendantEmpire.Application.Features.Maps.CampaignMaps.Classes;
using AscendantEmpire.BackendContracts.CampaignMaps;
using AscendantEmpire.Domain.Enums;
using AscendantEmpire.Domain.Enums.Intelligence;
using AscendantEmpire.Domain.Enums.Maps;
using AscendantEmpire.Domain.Enums.Structures;

namespace AscendantEmpire.Tests.Features.Intelligence;

/// <summary>
/// Pure unit tests for <see cref="StructureEventFolder"/> — no database, because it folds a timeline and witness answers it is handed.
/// </summary>
/// <remarks>
/// <para>
/// The witness answers are written out by hand, so each test says exactly which events a coalition saw. What makes an Observer a witness is pinned separately, in <c>WitnessResolutionTests</c>.
/// </para>
/// <para>
/// Player 1 is the acting Player and owns the Structure unless a test captures it. Coalition 2 is the watching Player; coalition 3 a second one where a test needs it.
/// </para>
/// </remarks>
[TestFixture]
internal class StructureEventFolderTests
{
    private const int _ACTING_PLAYER_ID = 1;
    private const int _WATCHING_COALITION_ID = 2;
    private const int _OTHER_COALITION_ID = 3;
    private const int _STRUCTURE_INSTANCE_ID = 50;

    private static StructureEvent Event(long probeKey, StructureHistoryEventType eventType, int turnSequence, double completionPercent = 40, int ownerPlayerId = _ACTING_PLAYER_ID, int? previousOwnerPlayerId = null)
    {
        return new StructureEvent
        {
            ProbeKey = probeKey,
            EventType = eventType,
            OwnerPlayerId = ownerPlayerId,
            PreviousOwnerPlayerId = previousOwnerPlayerId,
            CompletionPercent = completionPercent,
            TurnSequence = turnSequence
        };
    }

    private static StructureTimeline TimelineOf(params StructureEvent[] events)
    {
        return new StructureTimeline { StructureInstanceId = _STRUCTURE_INSTANCE_ID, StructureId = 7, CampaignMapTileId = 14, Location = new Coord(4, 1), Events = events };
    }

    private static Witness WitnessAt(int observerId, int distance, SightingFidelity fidelity = SightingFidelity.Detail)
    {
        return new Witness { ObserverId = observerId, RecordType = RecordType.Unit, OwnerPlayerId = _WATCHING_COALITION_ID, Location = new Coord(0, 0), Distance = distance, Fidelity = fidelity };
    }

    private static WitnessingCoalition Coalition(int coalitionId, params Witness[] witnessesNearestFirst)
    {
        return new WitnessingCoalition { CoalitionId = coalitionId, MemberPlayerIds = [coalitionId], Witnesses = witnessesNearestFirst };
    }

    /// <summary>
    /// Answers where the watching coalition saw exactly the given probe keys through one Observer - the end-of-turn probe included, when its key is listed - and nobody saw the rest.
    /// </summary>
    private static Dictionary<long, IReadOnlyCollection<WitnessingCoalition>> SeenAt(StructureTimeline timeline, params long[] seenProbeKeys)
    {
        Dictionary<long, IReadOnlyCollection<WitnessingCoalition>> answers = [];

        foreach (long probeKey in timeline.Events.Select(structureEvent => structureEvent.ProbeKey).Append(timeline.EndOfTurnProbeKey))
        {
            answers[probeKey] = seenProbeKeys.Contains(probeKey) ? [Coalition(_WATCHING_COALITION_ID, WitnessAt(observerId: 10, distance: 2))] : [];
        }

        return answers;
    }

    [Test, Description("Only what the coalition saw is flagged: a founding in view followed by a razing out of view is a founding, and the Structure is not known to have fallen.")]
    public void ShouldFlagOnlyTheEventsTheCoalitionSaw()
    {
        StructureTimeline timeline = TimelineOf(Event(1, StructureHistoryEventType.Founded, 3), Event(2, StructureHistoryEventType.Razed, 9));

        CoalitionStructureSighting sighting = StructureEventFolder.Fold(timeline, SeenAt(timeline, 1)).Single();

        using (Assert.EnterMultipleScope())
        {
            Assert.That(sighting.FoundedInView, Is.True);
            Assert.That(sighting.RazedInView, Is.False);
            Assert.That(sighting.Status, Is.EqualTo(KnownStructureStatus.Standing), "As far as the watcher knows, it stands.");
            Assert.That(sighting.FirstTurnSequence, Is.EqualTo(3));
            Assert.That(sighting.LastTurnSequence, Is.EqualTo(3));
            Assert.That(sighting.RaisesReport, Is.True);
        }
    }

    [Test, Description("A Structure founded and razed in view is one sighting carrying both, which is what lets the feed say it in one entry.")]
    public void ShouldFoldAFoundingAndARazingIntoOneSighting()
    {
        StructureTimeline timeline = TimelineOf(Event(1, StructureHistoryEventType.Founded, 3), Event(2, StructureHistoryEventType.Razed, 9));

        CoalitionStructureSighting sighting = StructureEventFolder.Fold(timeline, SeenAt(timeline, 1, 2)).Single();

        using (Assert.EnterMultipleScope())
        {
            Assert.That(sighting.FoundedInView, Is.True);
            Assert.That(sighting.RazedInView, Is.True);
            Assert.That(sighting.StillVisible, Is.False, "A razed Structure is not still there to be seen.");
            Assert.That(sighting.Status, Is.EqualTo(KnownStructureStatus.Razed));
            Assert.That(sighting.LastTurnSequence, Is.EqualTo(9));
        }
    }

    [Test, Description("Seeing construction advance without anything else raises no report - intermediate steps are not news - but still refreshes what the Player remembers.")]
    public void ShouldNotRaiseAReportWhenOnlyProgressWasSeen()
    {
        StructureTimeline timeline = TimelineOf(Event(1, StructureHistoryEventType.Progressed, 4, completionPercent: 60));

        CoalitionStructureSighting sighting = StructureEventFolder.Fold(timeline, SeenAt(timeline, 1, timeline.EndOfTurnProbeKey)).Single();

        using (Assert.EnterMultipleScope())
        {
            Assert.That(sighting.ProgressedInView, Is.True);
            Assert.That(sighting.RaisesReport, Is.False);
            Assert.That(sighting.CompletionPercent, Is.EqualTo(60));
            Assert.That(sighting.StillVisible, Is.True);
        }
    }

    [Test, Description("A founding and a completion seen in one Turn are both flagged, so the client can choose the completion sentence over the started one.")]
    public void ShouldFlagBothTheFoundingAndTheCompletionWhenBothWereSeen()
    {
        StructureTimeline timeline = TimelineOf(
            Event(1, StructureHistoryEventType.Founded, 1, completionPercent: 50),
            Event(2, StructureHistoryEventType.Progressed, 2, completionPercent: 80),
            Event(3, StructureHistoryEventType.Completed, 3, completionPercent: 100));

        CoalitionStructureSighting sighting = StructureEventFolder.Fold(timeline, SeenAt(timeline, 1, 2, 3, timeline.EndOfTurnProbeKey)).Single();

        using (Assert.EnterMultipleScope())
        {
            Assert.That(sighting.FoundedInView, Is.True);
            Assert.That(sighting.CompletedInView, Is.True);
            Assert.That(sighting.CompletionPercent, Is.EqualTo(100), "Progress as at the last event seen.");
        }
    }

    [Test, Description("A capture seen names who held the Structure before it, and the owner is whoever held it at the last event seen - here the captor, who then razed it.")]
    public void ShouldRecordThePreviousOwnerOfTheFirstCaptureAndTheOwnerAtTheLastEventSeen()
    {
        StructureTimeline timeline = TimelineOf(
            Event(1, StructureHistoryEventType.Captured, 2, ownerPlayerId: _ACTING_PLAYER_ID, previousOwnerPlayerId: _WATCHING_COALITION_ID),
            Event(2, StructureHistoryEventType.Razed, 5, ownerPlayerId: _ACTING_PLAYER_ID));

        CoalitionStructureSighting sighting = StructureEventFolder.Fold(timeline, SeenAt(timeline, 1, 2)).Single();

        using (Assert.EnterMultipleScope())
        {
            Assert.That(sighting.CapturedInView, Is.True);
            Assert.That(sighting.PreviousOwnerPlayerId, Is.EqualTo(_WATCHING_COALITION_ID));
            Assert.That(sighting.OwnerPlayerId, Is.EqualTo(_ACTING_PLAYER_ID));
        }
    }

    [Test, Description("A Structure seen changed and still in view as the Turn ended is still visible; one seen changed but out of view by the end is not.")]
    [TestCase(true)]
    [TestCase(false)]
    public void ShouldBeStillVisibleOnlyWhenTheEndOfTurnProbeWasSeen(bool seenAtTheEnd)
    {
        StructureTimeline timeline = TimelineOf(Event(1, StructureHistoryEventType.Founded, 3));

        long[] seen = seenAtTheEnd ? [1, timeline.EndOfTurnProbeKey] : [1];

        CoalitionStructureSighting sighting = StructureEventFolder.Fold(timeline, SeenAt(timeline, seen)).Single();

        Assert.That(sighting.StillVisible, Is.EqualTo(seenAtTheEnd));
    }

    [Test, Description("The sighting is as good as the best look any witness got, and the witness recorded is the nearest one at the first event seen.")]
    public void ShouldKeepTheBestFidelityAndTheNearestFirstWitness()
    {
        StructureTimeline timeline = TimelineOf(Event(1, StructureHistoryEventType.Founded, 3), Event(2, StructureHistoryEventType.Captured, 6, previousOwnerPlayerId: 9));

        Dictionary<long, IReadOnlyCollection<WitnessingCoalition>> answers = new()
        {
            [1] = [Coalition(_WATCHING_COALITION_ID, WitnessAt(observerId: 11, distance: 1, SightingFidelity.Movement), WitnessAt(observerId: 12, distance: 3, SightingFidelity.Movement))],
            [2] = [Coalition(_WATCHING_COALITION_ID, WitnessAt(observerId: 13, distance: 2, SightingFidelity.Owner))],
            [timeline.EndOfTurnProbeKey] = []
        };

        CoalitionStructureSighting sighting = StructureEventFolder.Fold(timeline, answers).Single();

        using (Assert.EnterMultipleScope())
        {
            Assert.That(sighting.Fidelity, Is.EqualTo(SightingFidelity.Owner));
            Assert.That(sighting.FirstWitness.ObserverId, Is.EqualTo(11));
        }
    }

    [Test, Description("Each coalition that saw something gets its own sighting in coalition order, and one that could only see the Structure at the end of the Turn saw nothing happen.")]
    public void ShouldGiveEachWatchingCoalitionItsOwnSightingAndNoneForOneThatSawNoEvent()
    {
        StructureTimeline timeline = TimelineOf(Event(1, StructureHistoryEventType.Founded, 3));

        Dictionary<long, IReadOnlyCollection<WitnessingCoalition>> answers = new()
        {
            [1] = [Coalition(_WATCHING_COALITION_ID, WitnessAt(observerId: 10, distance: 2))],
            [timeline.EndOfTurnProbeKey] = [Coalition(_WATCHING_COALITION_ID, WitnessAt(observerId: 10, distance: 2)), Coalition(_OTHER_COALITION_ID, WitnessAt(observerId: 20, distance: 1))]
        };

        IReadOnlyList<CoalitionStructureSighting> sightings = StructureEventFolder.Fold(timeline, answers);

        Assert.That(sightings.Select(sighting => sighting.CoalitionId), Is.EqualTo(new[] { _WATCHING_COALITION_ID }));
    }

    [Test, Description("A razed Structure is not probed at the end of the Turn, so a missing end-of-turn answer for it is expected rather than a mistake.")]
    public void ShouldNotNeedAnEndOfTurnAnswerForARazedStructure()
    {
        StructureTimeline timeline = TimelineOf(Event(1, StructureHistoryEventType.Razed, 3));

        Dictionary<long, IReadOnlyCollection<WitnessingCoalition>> answers = new() { [1] = [Coalition(_WATCHING_COALITION_ID, WitnessAt(observerId: 10, distance: 2))] };

        Assert.That(StructureEventFolder.Fold(timeline, answers), Has.Count.EqualTo(1));
    }

    [Test, Description("The witness service answers every probe, so a missing answer means the query was built wrongly, and it fails loudly rather than reporting nothing.")]
    [TestCase(false, Description = "An event with no answer.")]
    [TestCase(true, Description = "A standing Structure with no end-of-turn answer.")]
    public void ShouldThrowWhenAProbeHasNoAnswer(bool missingEndOfTurnAnswer)
    {
        StructureTimeline timeline = TimelineOf(Event(1, StructureHistoryEventType.Founded, 3));

        Dictionary<long, IReadOnlyCollection<WitnessingCoalition>> answers = SeenAt(timeline, 1);
        answers.Remove(missingEndOfTurnAnswer ? timeline.EndOfTurnProbeKey : 1);

        Assert.Throws<ArgumentException>(() => StructureEventFolder.Fold(timeline, answers));
    }
}
