using AscendantEmpire.Application.Features.Maps.CampaignMaps.Classes;
using AscendantEmpire.BackendContracts.CampaignMaps;
using AscendantEmpire.Domain.Entities.Battles;
using AscendantEmpire.Domain.Enums.Battle;

namespace AscendantEmpire.Application.Features.Intelligence.Classes;

/// <summary>
/// One thing that happened to an engagement during a Turn, as the fold works with it.
/// </summary>
internal sealed record EngagementEvent
{
    /// <summary>
    /// The witness probe key for the event: its <c>BattleHistory</c> row id, which is positive and unique.
    /// </summary>
    public required long ProbeKey { get; init; }

    public required BattleHistoryEventType EventType { get; init; }

    /// <summary>
    /// The side that joined or left; null on any other event.
    /// </summary>
    public required int? ParticipantPlayerId { get; init; }

    /// <summary>
    /// Why that side left; null on any other event.
    /// </summary>
    public required BattleExitReason? ExitReason { get; init; }

    /// <summary>
    /// What that side lost over its time in the engagement; null on any other event.
    /// </summary>
    public required int? TroopsLost { get; init; }

    /// <summary>
    /// Who was left holding the ground, on a conclusion; null on any other event, and on a conclusion nobody held the ground after.
    /// </summary>
    public required int? GroundHeldByPlayerId { get; init; }

    public required int TurnSequence { get; init; }
}

/// <summary>
/// Everything that happened to one engagement during one Turn, in order - the Battle counterpart of <see cref="StructureTimeline"/>.
/// </summary>
/// <remarks>
/// <para>
/// An engagement does not move either, so there are no tracks: every event is a point fact at the tile being fought over. What the fold does is gather, per coalition, which of these events that
/// coalition witnessed. See "What each producer emits" in <c>docs/design/intelligence-and-sightings.md</c>.
/// </para>
/// <para>
/// Alongside its events, an engagement still being fought at the end of the Turn is probed once more, as at the end of the Turn, to learn whether each coalition could still see it then.
/// </para>
/// </remarks>
internal sealed record BattleTimeline
{
    public required int BattleId { get; init; }

    public required int CampaignMapTileId { get; init; }

    public required Coord Location { get; init; }

    /// <summary>
    /// The engagement's events this Turn, by sequence and then by row id. Never empty.
    /// </summary>
    public required IReadOnlyList<EngagementEvent> Events { get; init; }

    /// <summary>
    /// Whether the engagement ended during the Turn. One that ended is being fought at the end of it by nobody, so it gets no end-of-turn probe.
    /// </summary>
    public bool HasConcluded => Events.Any(engagementEvent => engagementEvent.EventType == BattleHistoryEventType.Concluded);

    /// <summary>
    /// The probe key for "could this still be seen as the Turn ended": the Battle's id negated, which can never collide with a row id.
    /// </summary>
    public long EndOfTurnProbeKey => -BattleId;

    /// <summary>
    /// Builds one timeline per engagement from a Turn's history rows, ordered by Battle id so the same rows always fold in the same order.
    /// </summary>
    internal static IReadOnlyList<BattleTimeline> BuildAll(IEnumerable<BattleHistory> rows, CampaignMapGeometry geometry)
    {
        ArgumentNullException.ThrowIfNull(rows);
        ArgumentNullException.ThrowIfNull(geometry);

        return
        [
            .. rows
                .GroupBy(row => row.BattleId)
                .OrderBy(battleRows => battleRows.Key)
                .Select(battleRows => Build(battleRows, geometry))
        ];
    }

    private static BattleTimeline Build(IGrouping<int, BattleHistory> battleRows, CampaignMapGeometry geometry)
    {
        List<BattleHistory> orderedRows = [.. battleRows.OrderBy(row => row.TurnSequence).ThenBy(row => row.Id)];

        // An engagement is fought over one tile and never moves, so any row can say where it is. The first one is used.
        BattleHistory firstRow = orderedRows[0];

        return new BattleTimeline
        {
            BattleId = battleRows.Key,
            CampaignMapTileId = firstRow.CampaignMapTileId,
            Location = geometry.GetCoord(firstRow.CampaignMapTileId),
            Events =
            [
                .. orderedRows.Select(row => new EngagementEvent
                {
                    ProbeKey = row.Id,
                    EventType = row.EventType,
                    ParticipantPlayerId = row.ParticipantPlayerId,
                    ExitReason = row.ExitReason,
                    TroopsLost = row.TroopsLost,
                    GroundHeldByPlayerId = row.GroundHeldByPlayerId,
                    TurnSequence = row.TurnSequence
                })
            ]
        };
    }
}
