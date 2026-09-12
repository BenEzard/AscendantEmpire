using AscendantEmpire.Application.Features.Maps.CampaignMaps.Classes;
using AscendantEmpire.BackendContracts.CampaignMaps;
using AscendantEmpire.Domain.Entities.Structures;
using AscendantEmpire.Domain.Enums.Structures;

namespace AscendantEmpire.Application.Features.Intelligence.Classes;

/// <summary>
/// One thing that happened to a Structure during a Turn, as the fold works with it.
/// </summary>
internal sealed record StructureEvent
{
    /// <summary>
    /// The witness probe key for the event: its <c>StructureHistory</c> row id, which is positive and unique.
    /// </summary>
    public required long ProbeKey { get; init; }

    public required StructureHistoryEventType EventType { get; init; }

    /// <summary>
    /// Who held the Structure once the event had happened.
    /// </summary>
    public required int OwnerPlayerId { get; init; }

    /// <summary>
    /// Who held it before, on a capture; null on any other event.
    /// </summary>
    public required int? PreviousOwnerPlayerId { get; init; }

    /// <summary>
    /// How far built it was once the event had happened.
    /// </summary>
    public required double CompletionPercent { get; init; }

    public required int TurnSequence { get; init; }
}

/// <summary>
/// Everything that happened to one Structure during one Turn, in order - the Structure counterpart of <see cref="ForcePath"/>.
/// </summary>
/// <remarks>
/// <para>
/// A Structure does not move, so there are no tracks to fold: every event is a point fact at the same tile. What the fold does instead is gather, per coalition, which of these events that coalition
/// witnessed. See "What each producer emits" in <c>docs/design/intelligence-and-sightings.md</c>.
/// </para>
/// <para>
/// Alongside its events, a Structure still standing at the end of the Turn is probed once more, as at the end of the Turn, to learn whether each coalition could still see it then.
/// </para>
/// </remarks>
internal sealed record StructureTimeline
{
    public required int StructureInstanceId { get; init; }

    /// <summary>
    /// What kind of Structure it is.
    /// </summary>
    public required int StructureId { get; init; }

    public required int CampaignMapTileId { get; init; }

    public required Coord Location { get; init; }

    /// <summary>
    /// The Structure's events this Turn, by sequence and then by row id. Never empty.
    /// </summary>
    public required IReadOnlyList<StructureEvent> Events { get; init; }

    /// <summary>
    /// Whether the Structure was razed during the Turn. A razed Structure stands at the end of it for nobody, so it gets no end-of-turn probe.
    /// </summary>
    public bool IsRazed => Events.Any(structureEvent => structureEvent.EventType == StructureHistoryEventType.Razed);

    /// <summary>
    /// The probe key for "could this still be seen as the Turn ended": the Structure's id negated, which can never collide with a row id.
    /// </summary>
    public long EndOfTurnProbeKey => -StructureInstanceId;

    /// <summary>
    /// Builds one timeline per Structure from a Turn's history rows, ordered by Structure id so the same rows always fold in the same order.
    /// </summary>
    internal static IReadOnlyList<StructureTimeline> BuildAll(IEnumerable<StructureHistory> rows, CampaignMapGeometry geometry)
    {
        ArgumentNullException.ThrowIfNull(rows);
        ArgumentNullException.ThrowIfNull(geometry);

        return
        [
            .. rows
                .GroupBy(row => row.StructureInstanceId)
                .OrderBy(structureRows => structureRows.Key)
                .Select(structureRows => Build(structureRows, geometry))
        ];
    }

    private static StructureTimeline Build(IGrouping<int, StructureHistory> structureRows, CampaignMapGeometry geometry)
    {
        List<StructureHistory> orderedRows = [.. structureRows.OrderBy(row => row.TurnSequence).ThenBy(row => row.Id)];

        // A Structure never moves, and its kind never changes, so any row can say where and what it is. The first one is used.
        StructureHistory firstRow = orderedRows[0];

        return new StructureTimeline
        {
            StructureInstanceId = structureRows.Key,
            StructureId = firstRow.StructureId,
            CampaignMapTileId = firstRow.CampaignMapTileId,
            Location = geometry.GetCoord(firstRow.CampaignMapTileId),
            Events =
            [
                .. orderedRows.Select(row => new StructureEvent
                {
                    ProbeKey = row.Id,
                    EventType = row.EventType,
                    OwnerPlayerId = row.PlayerId,
                    PreviousOwnerPlayerId = row.PreviousOwnerPlayerId,
                    CompletionPercent = row.CompletionPercent,
                    TurnSequence = row.TurnSequence
                })
            ]
        };
    }
}
