using AscendantEmpire.Application.Features.Intelligence.Classes;
using AscendantEmpire.Application.Features.Maps.CampaignMaps.Classes;
using AscendantEmpire.BackendContracts.CampaignMaps;
using AscendantEmpire.Domain.Entities.Battles;
using AscendantEmpire.Domain.Enums.Battle;

namespace AscendantEmpire.Tests.Features.Intelligence;

/// <summary>
/// Pure unit tests for <see cref="BattleTimeline"/> — no database, because it only arranges history rows it is handed.
/// </summary>
/// <remarks>
/// The map is 10 x 8 with ids starting at 100.
/// </remarks>
[TestFixture]
internal class BattleTimelineTests
{
    private static readonly CampaignMapGeometry _GEOMETRY = new(CampaignMapOffsetId: 100, Width: 10, Height: 8, WrapsHorizontally: false, WrapsVertically: false);

    private static BattleHistory Row(long id, int battleId, BattleHistoryEventType eventType, int turnSequence, int x = 4, int y = 2)
    {
        bool isDeparture = eventType == BattleHistoryEventType.ParticipantLeft;

        return new BattleHistory
        {
            Id = id,
            GameId = 1,
            BattleId = battleId,
            EventType = eventType,
            ParticipantPlayerId = isDeparture || eventType == BattleHistoryEventType.ParticipantJoined ? 2 : null,
            ExitReason = isDeparture ? BattleExitReason.Routed : null,
            TroopsLost = isDeparture ? 30 : null,
            GroundHeldByPlayerId = eventType == BattleHistoryEventType.Concluded ? 3 : null,
            CampaignMapTileId = _GEOMETRY.GetCampaignMapId(new Coord(x, y)),
            PlayerTurnId = 40,
            TurnSequence = turnSequence
        };
    }

    [Test, Description("Events are ordered by the Turn's sequence and then by row, so the same history always folds the same way whatever order the rows were read in.")]
    public void ShouldOrderEachEngagementsEventsBySequenceThenRow()
    {
        BattleTimeline timeline = BattleTimeline.BuildAll(
            [
                Row(id: 12, battleId: 5, BattleHistoryEventType.Concluded, turnSequence: 9),
                Row(id: 11, battleId: 5, BattleHistoryEventType.ParticipantJoined, turnSequence: 4),
                Row(id: 10, battleId: 5, BattleHistoryEventType.Commenced, turnSequence: 4)
            ],
            _GEOMETRY).Single();

        Assert.That(timeline.Events.Select(engagementEvent => engagementEvent.ProbeKey), Is.EqualTo(new long[] { 10, 11, 12 }));
    }

    [Test, Description("Each engagement gets a timeline of its own, placed at the tile being fought over and ordered by Battle, so two Battles' events are never folded together.")]
    public void ShouldBuildOneTimelinePerEngagementAtItsTile()
    {
        IReadOnlyList<BattleTimeline> timelines = BattleTimeline.BuildAll(
            [
                Row(id: 20, battleId: 8, BattleHistoryEventType.Commenced, turnSequence: 1, x: 6, y: 3),
                Row(id: 21, battleId: 5, BattleHistoryEventType.Commenced, turnSequence: 2, x: 1, y: 1)
            ],
            _GEOMETRY);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(timelines.Select(timeline => timeline.BattleId), Is.EqualTo(new[] { 5, 8 }));
            Assert.That(timelines[0].Location, Is.EqualTo(new Coord(1, 1)));
            Assert.That(timelines[0].CampaignMapTileId, Is.EqualTo(_GEOMETRY.GetCampaignMapId(new Coord(1, 1))));
            Assert.That(timelines[1].Location, Is.EqualTo(new Coord(6, 3)));
        }
    }

    [Test, Description("A concluded engagement is marked concluded, and an engagement's end-of-turn probe key is negative so it can never collide with a history row id.")]
    public void ShouldMarkConclusionAndKeyTheEndOfTurnProbeApartFromRowIds()
    {
        IReadOnlyList<BattleTimeline> timelines = BattleTimeline.BuildAll(
            [
                Row(id: 30, battleId: 5, BattleHistoryEventType.Concluded, turnSequence: 3),
                Row(id: 31, battleId: 6, BattleHistoryEventType.ParticipantJoined, turnSequence: 4)
            ],
            _GEOMETRY);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(timelines[0].HasConcluded, Is.True);
            Assert.That(timelines[1].HasConcluded, Is.False, "An engagement whose only row this Turn is a join is still being fought.");
            Assert.That(timelines[1].EndOfTurnProbeKey, Is.EqualTo(-7), "The complement of the Battle id, which is negative for every id and so never a row's key.");
        }
    }

    [Test, Description("An engagement with id 0 still gets an end-of-turn key of its own, because a negated 0 would be the key of a row with id 0 and the two answers would collide.")]
    public void ShouldKeepTheEndOfTurnKeyDistinctWhenTheBattleIdIsZero()
    {
        BattleTimeline timeline = BattleTimeline
            .BuildAll([Row(id: 0, battleId: 0, BattleHistoryEventType.ParticipantJoined, turnSequence: 1)], _GEOMETRY)
            .Single();

        Assert.That(timeline.EndOfTurnProbeKey, Is.Not.EqualTo(timeline.Events.Single().ProbeKey));
    }

    [Test, Description("A departure's reason and losses, and a conclusion's ground-holder, come through onto the event so the fold never re-reads the history rows.")]
    public void ShouldCarryEachEventsOwnFieldsOntoTheTimeline()
    {
        BattleTimeline timeline = BattleTimeline.BuildAll(
            [
                Row(id: 40, battleId: 5, BattleHistoryEventType.ParticipantLeft, turnSequence: 2),
                Row(id: 41, battleId: 5, BattleHistoryEventType.Concluded, turnSequence: 3)
            ],
            _GEOMETRY).Single();

        using (Assert.EnterMultipleScope())
        {
            Assert.That(timeline.Events[0].ParticipantPlayerId, Is.EqualTo(2));
            Assert.That(timeline.Events[0].ExitReason, Is.EqualTo(BattleExitReason.Routed));
            Assert.That(timeline.Events[0].TroopsLost, Is.EqualTo(30));
            Assert.That(timeline.Events[1].GroundHeldByPlayerId, Is.EqualTo(3));
            Assert.That(timeline.Events[1].TroopsLost, Is.Null, "Losses belong to a side's own departure row, never to the conclusion.");
        }
    }
}
