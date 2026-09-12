using AscendantEmpire.Application.Features.Maps.CampaignMaps.Classes;
using AscendantEmpire.Domain.Enums.Battle;
using AscendantEmpire.Domain.Enums.Maps;

namespace AscendantEmpire.Application.Features.Intelligence.Classes;

/// <summary>
/// Everything one coalition saw happen at one engagement during a Turn - the unit a combat report is raised from.
/// </summary>
/// <remarks>
/// <para>
/// One record for the whole Turn rather than one per event, so fighting breaking out and ending in view is one entry in the feed. The flags say which events were seen; the client chooses the
/// sentence from them.
/// </para>
/// <para>
/// Everything here is what the coalition <b>witnessed</b>, never the engagement's own truth. A coalition that watched one side break and then lost sight of the field learns that side's losses and
/// no more, and one that never saw the end is not told who was left holding the ground.
/// </para>
/// </remarks>
internal sealed record CoalitionEngagementSighting
{
    public required int CoalitionId { get; init; }

    /// <summary>
    /// The Players the coalition's report fans out to, one each.
    /// </summary>
    public required IReadOnlyCollection<int> MemberPlayerIds { get; init; }

    public required bool CommencedInView { get; init; }

    public required bool ConcludedInView { get; init; }

    /// <summary>
    /// Whether a side was seen joining the engagement after it had started, or being there as it started.
    /// </summary>
    public required bool SideJoinedInView { get; init; }

    /// <summary>
    /// Whether a side was seen leaving, whether mid-fight or as part of the conclusion.
    /// </summary>
    public required bool SideLeftInView { get; init; }

    /// <summary>
    /// The sequence of the first event seen.
    /// </summary>
    public required int FirstTurnSequence { get; init; }

    /// <summary>
    /// The sequence of the last event seen.
    /// </summary>
    public required int LastTurnSequence { get; init; }

    /// <summary>
    /// Who was left holding the ground, or null when the coalition did not see the engagement end - or saw it end with nobody holding it.
    /// </summary>
    public required int? GroundHeldByPlayerId { get; init; }

    /// <summary>
    /// The losses across the departures the coalition witnessed, or null when it witnessed none.
    /// </summary>
    /// <remarks>
    /// Summed rather than taken from a battle-wide total, which is the whole reason losses sit on each side's own row: the sum is over what this coalition actually watched happen.
    /// </remarks>
    public required int? TroopsLost { get; init; }

    /// <summary>
    /// Whether the engagement was still being fought and still in the coalition's view as the Turn ended.
    /// </summary>
    public required bool StillVisible { get; init; }

    /// <summary>
    /// The most any of the coalition's witnesses made out at any event seen.
    /// </summary>
    public required SightingFidelity Fidelity { get; init; }

    /// <summary>
    /// The nearest witness at the first event seen.
    /// </summary>
    public required Witness FirstWitness { get; init; }
}

/// <summary>
/// Folds an engagement's timeline and the witness service's answers into what each coalition saw of it.
/// </summary>
/// <remarks>
/// <para>
/// The Battle counterpart of <see cref="StructureEventFolder"/>, and pure in the same way - no database.
/// </para>
/// <para>
/// <b>Every sighting raises a report.</b> There is deliberately no <c>RaisesReport</c> here, unlike the Structure folder: an engagement has no equivalent of a construction step, which refreshes a
/// memory without being news. Anything witnessed at a battle is worth telling the watcher about.
/// </para>
/// <para>
/// <b>Per coalition, not per observer.</b> Three of a Player's Units watching the same valley give that Player one sighting, with the nearest of them at the first event kept as the witness.
/// </para>
/// </remarks>
internal static class EngagementEventFolder
{
    /// <summary>
    /// Works out what every coalition saw happen at one engagement.
    /// </summary>
    /// <param name="answersByProbeKey">
    /// The witness service's answers, which must hold an entry for every event's probe key and, unless the engagement concluded, for its <see cref="BattleTimeline.EndOfTurnProbeKey"/>.
    /// </param>
    /// <returns>One entry per coalition that saw at least one event, in coalition id order. Empty when nobody saw anything.</returns>
    /// <exception cref="ArgumentException">A probe has no answer. The witness service answers every probe, so a missing one means the caller built the query wrongly.</exception>
    internal static IReadOnlyList<CoalitionEngagementSighting> Fold(BattleTimeline timeline, IReadOnlyDictionary<long, IReadOnlyCollection<WitnessingCoalition>> answersByProbeKey)
    {
        ArgumentNullException.ThrowIfNull(timeline);
        ArgumentNullException.ThrowIfNull(answersByProbeKey);

        // For each coalition: who is in it, and the events it saw with its answer for each, in the timeline's order.
        Dictionary<int, (IReadOnlyCollection<int> MemberPlayerIds, List<(EngagementEvent Event, WitnessingCoalition Answer)> SeenEvents)> sightingsByCoalitionId = [];

        foreach (EngagementEvent engagementEvent in timeline.Events)
        {
            foreach (WitnessingCoalition coalition in AnswerFor(engagementEvent.ProbeKey, timeline, answersByProbeKey))
            {
                if (sightingsByCoalitionId.TryGetValue(coalition.CoalitionId, out var sightings) == false)
                {
                    sightings = (coalition.MemberPlayerIds, []);
                    sightingsByCoalitionId[coalition.CoalitionId] = sightings;
                }

                sightings.SeenEvents.Add((engagementEvent, coalition));
            }
        }

        // Who could still see the tile as the Turn ended. An engagement that ended was not there to be seen, so it is not probed and nobody sees it still being fought.
        HashSet<int> coalitionIdsSeeingItAtTheEnd = timeline.HasConcluded
            ? []
            : [.. AnswerFor(timeline.EndOfTurnProbeKey, timeline, answersByProbeKey).Select(coalition => coalition.CoalitionId)];

        List<CoalitionEngagementSighting> result = [];

        foreach ((int coalitionId, (IReadOnlyCollection<int> memberPlayerIds, List<(EngagementEvent Event, WitnessingCoalition Answer)> seenEvents)) in sightingsByCoalitionId.OrderBy(entry => entry.Key))
        {
            EngagementEvent? conclusionSeen = seenEvents.Select(seen => seen.Event).FirstOrDefault(seenEvent => seenEvent.EventType == BattleHistoryEventType.Concluded);

            int[] lossesSeen =
            [
                .. seenEvents
                    .Where(seen => seen.Event.EventType == BattleHistoryEventType.ParticipantLeft)
                    .Select(seen => seen.Event.TroopsLost ?? 0)
            ];

            result.Add(new CoalitionEngagementSighting
            {
                CoalitionId = coalitionId,
                MemberPlayerIds = memberPlayerIds,
                CommencedInView = WasSeen(seenEvents, BattleHistoryEventType.Commenced),
                ConcludedInView = conclusionSeen is not null,
                SideJoinedInView = WasSeen(seenEvents, BattleHistoryEventType.ParticipantJoined),
                SideLeftInView = lossesSeen.Length > 0,
                FirstTurnSequence = seenEvents[0].Event.TurnSequence,
                LastTurnSequence = seenEvents[^1].Event.TurnSequence,
                GroundHeldByPlayerId = conclusionSeen?.GroundHeldByPlayerId,
                // Null rather than zero when no departure was seen, so that "I saw nobody break" and "I saw a side break losing nobody" stay different answers.
                TroopsLost = lossesSeen.Length == 0 ? null : lossesSeen.Sum(),
                StillVisible = coalitionIdsSeeingItAtTheEnd.Contains(coalitionId),
                Fidelity = seenEvents.Max(seen => seen.Answer.BestFidelity),
                // The witness service lists witnesses nearest first.
                FirstWitness = seenEvents[0].Answer.Witnesses.First()
            });
        }

        return result;
    }

    private static IReadOnlyCollection<WitnessingCoalition> AnswerFor(long probeKey, BattleTimeline timeline, IReadOnlyDictionary<long, IReadOnlyCollection<WitnessingCoalition>> answersByProbeKey)
    {
        if (answersByProbeKey.TryGetValue(probeKey, out IReadOnlyCollection<WitnessingCoalition>? coalitions) == false)
        {
            throw new ArgumentException($"There is no witness answer for probe key {probeKey} of Battle Id {timeline.BattleId}.", nameof(answersByProbeKey));
        }

        return coalitions;
    }

    private static bool WasSeen(List<(EngagementEvent Event, WitnessingCoalition Answer)> seenEvents, BattleHistoryEventType eventType)
    {
        return seenEvents.Any(seen => seen.Event.EventType == eventType);
    }
}
