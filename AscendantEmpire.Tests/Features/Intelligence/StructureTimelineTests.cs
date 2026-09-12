using AscendantEmpire.Application.Features.Intelligence.Classes;
using AscendantEmpire.Application.Features.Maps.CampaignMaps.Classes;
using AscendantEmpire.BackendContracts.CampaignMaps;
using AscendantEmpire.Domain.Entities.Structures;
using AscendantEmpire.Domain.Enums.Structures;

namespace AscendantEmpire.Tests.Features.Intelligence;

/// <summary>
/// Pure unit tests for <see cref="StructureTimeline"/> — no database, because it only arranges history rows it is handed.
/// </summary>
/// <remarks>
/// The map is 10 x 8 with ids starting at 100.
/// </remarks>
[TestFixture]
internal class StructureTimelineTests
{
    private static readonly CampaignMapGeometry _GEOMETRY = new(CampaignMapOffsetId: 100, Width: 10, Height: 8, WrapsHorizontally: false, WrapsVertically: false);

    private static StructureHistory Row(long id, int structureInstanceId, StructureHistoryEventType eventType, int turnSequence, int x = 4, int y = 2)
    {
        return new StructureHistory
        {
            Id = id,
            GameId = 1,
            StructureInstanceId = structureInstanceId,
            StructureId = 7,
            PlayerId = 2,
            PreviousOwnerPlayerId = eventType == StructureHistoryEventType.Captured ? 3 : null,
            EventType = eventType,
            CompletionPercent = 50,
            CampaignMapTileId = _GEOMETRY.GetCampaignMapId(new Coord(x, y)),
            PlayerTurnId = 40,
            TurnSequence = turnSequence
        };
    }

    [Test, Description("Events are ordered by the Turn's sequence and then by row, so the same history always folds the same way whatever order the rows were read in.")]
    public void ShouldOrderEachStructuresEventsBySequenceThenRow()
    {
        StructureTimeline timeline = StructureTimeline.BuildAll(
            [
                Row(id: 12, structureInstanceId: 5, StructureHistoryEventType.Razed, turnSequence: 9),
                Row(id: 11, structureInstanceId: 5, StructureHistoryEventType.Progressed, turnSequence: 4),
                Row(id: 10, structureInstanceId: 5, StructureHistoryEventType.Founded, turnSequence: 4)
            ],
            _GEOMETRY).Single();

        Assert.That(timeline.Events.Select(structureEvent => structureEvent.ProbeKey), Is.EqualTo(new long[] { 10, 11, 12 }));
    }

    [Test, Description("Each Structure gets a timeline of its own, placed at its tile and ordered by Structure, so two Structures' events are never folded together.")]
    public void ShouldBuildOneTimelinePerStructureAtItsTile()
    {
        IReadOnlyList<StructureTimeline> timelines = StructureTimeline.BuildAll(
            [
                Row(id: 20, structureInstanceId: 8, StructureHistoryEventType.Founded, turnSequence: 1, x: 6, y: 3),
                Row(id: 21, structureInstanceId: 5, StructureHistoryEventType.Founded, turnSequence: 2, x: 1, y: 1)
            ],
            _GEOMETRY);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(timelines.Select(timeline => timeline.StructureInstanceId), Is.EqualTo(new[] { 5, 8 }));
            Assert.That(timelines[0].Location, Is.EqualTo(new Coord(1, 1)));
            Assert.That(timelines[0].CampaignMapTileId, Is.EqualTo(_GEOMETRY.GetCampaignMapId(new Coord(1, 1))));
            Assert.That(timelines[1].Location, Is.EqualTo(new Coord(6, 3)));
            Assert.That(timelines[0].StructureId, Is.EqualTo(7));
        }
    }

    [Test, Description("A razed Structure is marked razed, and a Structure's end-of-turn probe key is negative so it can never collide with a history row id.")]
    public void ShouldMarkRazingAndKeyTheEndOfTurnProbeApartFromRowIds()
    {
        IReadOnlyList<StructureTimeline> timelines = StructureTimeline.BuildAll(
            [
                Row(id: 30, structureInstanceId: 5, StructureHistoryEventType.Razed, turnSequence: 3),
                Row(id: 31, structureInstanceId: 6, StructureHistoryEventType.Captured, turnSequence: 4)
            ],
            _GEOMETRY);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(timelines[0].IsRazed, Is.True);
            Assert.That(timelines[1].IsRazed, Is.False);
            Assert.That(timelines[1].EndOfTurnProbeKey, Is.EqualTo(-6));
            Assert.That(timelines[1].Events.Single().PreviousOwnerPlayerId, Is.EqualTo(3), "A capture's previous owner comes through from its row.");
        }
    }
}
