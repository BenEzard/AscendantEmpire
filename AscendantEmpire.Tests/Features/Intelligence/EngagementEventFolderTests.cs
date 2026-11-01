using AscendantEmpire.Application.Features.Intelligence.Classes;
using AscendantEmpire.Application.Features.Maps.CampaignMaps.Classes;
using AscendantEmpire.BackendContracts.CampaignMaps;
using AscendantEmpire.Domain.Enums;
using AscendantEmpire.Domain.Enums.Battle;
using AscendantEmpire.Domain.Enums.Maps;

namespace AscendantEmpire.Tests.Features.Intelligence;

/// <summary>
/// Pure unit tests for <see cref="EngagementEventFolder"/> — no database, because it folds a timeline and witness answers it is handed.
/// </summary>
/// <remarks>
/// <para>
/// The witness answers are written out by hand, so each test says exactly which events a coalition saw. What makes an Observer a witness is pinned separately, in <c>WitnessResolutionTests</c>.
/// </para>
/// <para>
/// Players 1 and 4 are fighting. Coalition 2 is the watching Player; coalition 3 a second one where a test needs it.
/// </para>
/// </remarks>
[TestFixture]
internal class EngagementEventFolderTests
{
    private const int _ATTACKING_PLAYER_ID = 1;
    private const int _DEFENDING_PLAYER_ID = 4;
    private const int _WATCHING_COALITION_ID = 2;
    private const int _OTHER_COALITION_ID = 3;
    private const int _BATTLE_ID = 50;

    private static EngagementEvent Event(
        long probeKey,
        BattleHistoryEventType eventType,
        int turnSequence,
        int? participantPlayerId = null,
        int? troopsLost = null,
        int? groundHeldByPlayerId = null)
    {
        return new EngagementEvent
        {
            ProbeKey = probeKey,
            EventType = eventType,
            ParticipantPlayerId = participantPlayerId,
            ExitReason = eventType == BattleHistoryEventType.ParticipantLeft ? BattleExitReason.Routed : null,
            TroopsLost = troopsLost,
            GroundHeldByPlayerId = groundHeldByPlayerId,
            TurnSequence = turnSequence
        };
    }

    private static BattleTimeline TimelineOf(params EngagementEvent[] events)
    {
        return new BattleTimeline { BattleId = _BATTLE_ID, CampaignMapTileId = 14, Location = new Coord(4, 1), Events = events };
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
    private static Dictionary<long, IReadOnlyCollection<WitnessingCoalition>> SeenAt(BattleTimeline timeline, params long[] seenProbeKeys)
    {
        Dictionary<long, IReadOnlyCollection<WitnessingCoalition>> answers = [];

        foreach (long probeKey in timeline.Events.Select(engagementEvent => engagementEvent.ProbeKey).Append(timeline.EndOfTurnProbeKey))
        {
            answers[probeKey] = seenProbeKeys.Contains(probeKey) ? [Coalition(_WATCHING_COALITION_ID, WitnessAt(observerId: 10, distance: 2))] : [];
        }

        return answers;
    }

    [Test, Description("Only what the coalition saw is flagged: fighting seen breaking out and then ending out of view is a commencement, and the watcher is not told it is over.")]
    public void ShouldFlagOnlyTheEventsTheCoalitionSaw()
    {
        BattleTimeline timeline = TimelineOf(
            Event(1, BattleHistoryEventType.Commenced, 3),
            Event(2, BattleHistoryEventType.Concluded, 9, groundHeldByPlayerId: _ATTACKING_PLAYER_ID));

        CoalitionEngagementSighting sighting = EngagementEventFolder.Fold(timeline, SeenAt(timeline, 1)).Single();

        using (Assert.EnterMultipleScope())
        {
            Assert.That(sighting.CommencedInView, Is.True);
            Assert.That(sighting.ConcludedInView, Is.False);
            Assert.That(sighting.GroundHeldByPlayerId, Is.Null, "Who was left holding the ground is only known to a watcher who saw it end.");
            Assert.That(sighting.FirstTurnSequence, Is.EqualTo(3));
            Assert.That(sighting.LastTurnSequence, Is.EqualTo(3));
        }
    }

    [Test, Description("Everything one coalition saw at one engagement folds into a single sighting, which is what lets the feed say it in one entry.")]
    public void ShouldFoldAWholeEngagementIntoOneSighting()
    {
        BattleTimeline timeline = TimelineOf(
            Event(1, BattleHistoryEventType.Commenced, 3),
            Event(2, BattleHistoryEventType.ParticipantJoined, 4, participantPlayerId: _DEFENDING_PLAYER_ID),
            Event(3, BattleHistoryEventType.ParticipantLeft, 8, participantPlayerId: _DEFENDING_PLAYER_ID, troopsLost: 40),
            Event(4, BattleHistoryEventType.Concluded, 9, groundHeldByPlayerId: _ATTACKING_PLAYER_ID));

        IReadOnlyList<CoalitionEngagementSighting> sightings = EngagementEventFolder.Fold(timeline, SeenAt(timeline, 1, 2, 3, 4));

        using (Assert.EnterMultipleScope())
        {
            Assert.That(sightings, Has.Count.EqualTo(1));
            Assert.That(sightings[0].CommencedInView, Is.True);
            Assert.That(sightings[0].SideJoinedInView, Is.True);
            Assert.That(sightings[0].SideLeftInView, Is.True);
            Assert.That(sightings[0].ConcludedInView, Is.True);
            Assert.That(sightings[0].GroundHeldByPlayerId, Is.EqualTo(_ATTACKING_PLAYER_ID));
            Assert.That(sightings[0].LastTurnSequence, Is.EqualTo(9));
        }
    }

    [Test, Description("Losses are summed over only the departures the coalition watched, so a watcher who saw one side break is told that side's losses and no more.")]
    public void ShouldSumOnlyTheLossesOfTheDeparturesSeen()
    {
        BattleTimeline timeline = TimelineOf(
            Event(1, BattleHistoryEventType.ParticipantLeft, 4, participantPlayerId: _DEFENDING_PLAYER_ID, troopsLost: 40),
            Event(2, BattleHistoryEventType.ParticipantLeft, 5, participantPlayerId: _ATTACKING_PLAYER_ID, troopsLost: 90),
            Event(3, BattleHistoryEventType.Concluded, 6));

        CoalitionEngagementSighting sighting = EngagementEventFolder.Fold(timeline, SeenAt(timeline, 1, 3)).Single();

        using (Assert.EnterMultipleScope())
        {
            Assert.That(sighting.TroopsLost, Is.EqualTo(40), "The side that broke out of view took its losses with it.");
            Assert.That(sighting.SideLeftInView, Is.True);
            Assert.That(sighting.ConcludedInView, Is.True);
        }
    }

    [Test, Description("Losses are null rather than zero when no departure was seen, so \"I saw nobody break\" stays a different answer from \"a side broke losing nobody\".")]
    public void ShouldLeaveLossesNullWhenNoDepartureWasSeen()
    {
        BattleTimeline timeline = TimelineOf(
            Event(1, BattleHistoryEventType.Commenced, 3),
            Event(2, BattleHistoryEventType.ParticipantLeft, 8, participantPlayerId: _DEFENDING_PLAYER_ID, troopsLost: 40));

        CoalitionEngagementSighting sighting = EngagementEventFolder.Fold(timeline, SeenAt(timeline, 1)).Single();

        using (Assert.EnterMultipleScope())
        {
            Assert.That(sighting.TroopsLost, Is.Null);
            Assert.That(sighting.SideLeftInView, Is.False);
        }
    }

    [Test, Description("A departure seen with no losses at all reports zero rather than nothing, since watching a side walk away intact is still something the watcher saw.")]
    public void ShouldReportZeroLossesForADepartureSeenThatCostNobody()
    {
        BattleTimeline timeline = TimelineOf(Event(1, BattleHistoryEventType.ParticipantLeft, 4, participantPlayerId: _DEFENDING_PLAYER_ID, troopsLost: 0));

        CoalitionEngagementSighting sighting = EngagementEventFolder.Fold(timeline, SeenAt(timeline, 1, timeline.EndOfTurnProbeKey)).Single();

        using (Assert.EnterMultipleScope())
        {
            Assert.That(sighting.TroopsLost, Is.Zero);
            Assert.That(sighting.SideLeftInView, Is.True);
        }
    }

    [Test, Description("An engagement still being fought and still in view as the Turn ended is still visible; one out of view by the end is not.")]
    [TestCase(true)]
    [TestCase(false)]
    public void ShouldBeStillVisibleOnlyWhenTheEndOfTurnProbeWasSeen(bool seenAtTheEnd)
    {
        BattleTimeline timeline = TimelineOf(Event(1, BattleHistoryEventType.Commenced, 3));

        long[] seen = seenAtTheEnd ? [1, timeline.EndOfTurnProbeKey] : [1];

        CoalitionEngagementSighting sighting = EngagementEventFolder.Fold(timeline, SeenAt(timeline, seen)).Single();

        Assert.That(sighting.StillVisible, Is.EqualTo(seenAtTheEnd));
    }

    [Test, Description("A concluded engagement is never still visible, however well the tile is watched: the fighting a watcher would have to still see is over.")]
    public void ShouldNotBeStillVisibleOnceTheEngagementHasConcluded()
    {
        BattleTimeline timeline = TimelineOf(
            Event(1, BattleHistoryEventType.Commenced, 3),
            Event(2, BattleHistoryEventType.Concluded, 9));

        Dictionary<long, IReadOnlyCollection<WitnessingCoalition>> answers = new()
        {
            [1] = [Coalition(_WATCHING_COALITION_ID, WitnessAt(observerId: 10, distance: 2))],
            [2] = [Coalition(_WATCHING_COALITION_ID, WitnessAt(observerId: 10, distance: 2))],
            [timeline.EndOfTurnProbeKey] = [Coalition(_WATCHING_COALITION_ID, WitnessAt(observerId: 10, distance: 2))]
        };

        Assert.That(EngagementEventFolder.Fold(timeline, answers).Single().StillVisible, Is.False);
    }

    [Test, Description("The sighting is as good as the best look any witness got, and the witness recorded is the nearest one at the first event seen.")]
    public void ShouldKeepTheBestFidelityAndTheNearestFirstWitness()
    {
        BattleTimeline timeline = TimelineOf(
            Event(1, BattleHistoryEventType.Commenced, 3),
            Event(2, BattleHistoryEventType.Concluded, 6));

        Dictionary<long, IReadOnlyCollection<WitnessingCoalition>> answers = new()
        {
            [1] = [Coalition(_WATCHING_COALITION_ID, WitnessAt(observerId: 11, distance: 1, SightingFidelity.Movement), WitnessAt(observerId: 12, distance: 3, SightingFidelity.Movement))],
            [2] = [Coalition(_WATCHING_COALITION_ID, WitnessAt(observerId: 13, distance: 2, SightingFidelity.Owner))]
        };

        CoalitionEngagementSighting sighting = EngagementEventFolder.Fold(timeline, answers).Single();

        using (Assert.EnterMultipleScope())
        {
            Assert.That(sighting.Fidelity, Is.EqualTo(SightingFidelity.Owner));
            Assert.That(sighting.FirstWitness.ObserverId, Is.EqualTo(11));
        }
    }

    [Test, Description("Each coalition that saw something gets its own sighting in coalition order, and one that could only see the tile at the end of the Turn saw nothing happen.")]
    public void ShouldGiveEachWatchingCoalitionItsOwnSightingAndNoneForOneThatSawNoEvent()
    {
        BattleTimeline timeline = TimelineOf(Event(1, BattleHistoryEventType.Commenced, 3));

        Dictionary<long, IReadOnlyCollection<WitnessingCoalition>> answers = new()
        {
            [1] = [Coalition(_WATCHING_COALITION_ID, WitnessAt(observerId: 10, distance: 2))],
            [timeline.EndOfTurnProbeKey] = [Coalition(_WATCHING_COALITION_ID, WitnessAt(observerId: 10, distance: 2)), Coalition(_OTHER_COALITION_ID, WitnessAt(observerId: 20, distance: 1))]
        };

        IReadOnlyList<CoalitionEngagementSighting> sightings = EngagementEventFolder.Fold(timeline, answers);

        Assert.That(sightings.Select(sighting => sighting.CoalitionId), Is.EqualTo(new[] { _WATCHING_COALITION_ID }));
    }

    [Test, Description("A concluded engagement is not probed at the end of the Turn, so a missing end-of-turn answer for it is expected rather than a mistake.")]
    public void ShouldNotNeedAnEndOfTurnAnswerForAConcludedEngagement()
    {
        BattleTimeline timeline = TimelineOf(Event(1, BattleHistoryEventType.Concluded, 3));

        Dictionary<long, IReadOnlyCollection<WitnessingCoalition>> answers = new() { [1] = [Coalition(_WATCHING_COALITION_ID, WitnessAt(observerId: 10, distance: 2))] };

        Assert.That(EngagementEventFolder.Fold(timeline, answers), Has.Count.EqualTo(1));
    }

    [Test, Description("The witness service answers every probe, so a missing answer means the query was built wrongly, and it fails loudly rather than reporting nothing.")]
    [TestCase(false, Description = "An event with no answer.")]
    [TestCase(true, Description = "An engagement still being fought with no end-of-turn answer.")]
    public void ShouldThrowWhenAProbeHasNoAnswer(bool missingEndOfTurnAnswer)
    {
        BattleTimeline timeline = TimelineOf(Event(1, BattleHistoryEventType.Commenced, 3));

        Dictionary<long, IReadOnlyCollection<WitnessingCoalition>> answers = SeenAt(timeline, 1);
        answers.Remove(missingEndOfTurnAnswer ? timeline.EndOfTurnProbeKey : 1);

        Assert.Throws<ArgumentException>(() => EngagementEventFolder.Fold(timeline, answers));
    }
}
