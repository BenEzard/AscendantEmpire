using AscendantEmpire.Application.Features.Maps.CampaignMaps.Classes;
using AscendantEmpire.Domain.Enums.Intelligence;
using AscendantEmpire.Domain.Enums.Maps;
using AscendantEmpire.Domain.Enums.Structures;

namespace AscendantEmpire.Application.Features.Intelligence.Classes;

/// <summary>
/// Everything one coalition saw happen to one Structure during a Turn - the unit a structure report and a known-Structure memory are raised from.
/// </summary>
/// <remarks>
/// <para>
/// One record for the whole Turn rather than one per event, so founding a village and razing it in view is one entry in the feed. The flags say which events were seen; the client chooses the
/// sentence from them.
/// </para>
/// <para>
/// The observed state - owner and progress - is as at the <b>last</b> event the coalition witnessed. Anything that happened to the Structure afterwards, out of their sight, is not known to them.
/// </para>
/// </remarks>
internal sealed record CoalitionStructureSighting
{
    public required int CoalitionId { get; init; }

    /// <summary>
    /// The Players the coalition's report and memory fan out to, one each.
    /// </summary>
    public required IReadOnlyCollection<int> MemberPlayerIds { get; init; }

    public required bool FoundedInView { get; init; }

    public required bool CompletedInView { get; init; }

    public required bool CapturedInView { get; init; }

    public required bool RazedInView { get; init; }

    /// <summary>
    /// Whether a construction step that did not finish the Structure was seen. It refreshes the memory but never raises a report on its own.
    /// </summary>
    public required bool ProgressedInView { get; init; }

    /// <summary>
    /// The sequence of the first event seen.
    /// </summary>
    public required int FirstTurnSequence { get; init; }

    /// <summary>
    /// The sequence of the last event seen, which is what the known-Structure memory is dated by.
    /// </summary>
    public required int LastTurnSequence { get; init; }

    /// <summary>
    /// Who held the Structure at the last event seen.
    /// </summary>
    public required int OwnerPlayerId { get; init; }

    /// <summary>
    /// Who held it before the first capture seen, or null when no capture was seen.
    /// </summary>
    public required int? PreviousOwnerPlayerId { get; init; }

    /// <summary>
    /// How far built it was at the last event seen.
    /// </summary>
    public required double CompletionPercent { get; init; }

    /// <summary>
    /// Whether the Structure still stood and was still in the coalition's view as the Turn ended.
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

    /// <summary>
    /// Whether anything the feed reports was seen: a founding, a completion, a capture or a razing. Construction progress alone only refreshes the memory.
    /// </summary>
    public bool RaisesReport => FoundedInView || CompletedInView || CapturedInView || RazedInView;

    /// <summary>
    /// What the coalition last knew of the Structure. Razing ends a Structure's history, so a razing seen is always the last thing seen.
    /// </summary>
    public KnownStructureStatus Status => RazedInView ? KnownStructureStatus.Razed : KnownStructureStatus.Standing;
}

/// <summary>
/// Folds a Structure's timeline and the witness service's answers into what each coalition saw of it.
/// </summary>
/// <remarks>
/// <para>
/// The Structure counterpart of <see cref="ForceTrackFolder"/>, and pure in the same way - no database. There are no tracks and no cap: a Structure's events all happen at one tile, and a Turn gives
/// a Structure at most a handful of them.
/// </para>
/// <para>
/// <b>Per coalition, not per observer.</b> Three of a Player's Units watching the same village give that Player one sighting, with the nearest of them at the first event kept as the witness.
/// </para>
/// </remarks>
internal static class StructureEventFolder
{
    /// <summary>
    /// Works out what every coalition saw happen to one Structure.
    /// </summary>
    /// <param name="answersByProbeKey">
    /// The witness service's answers, which must hold an entry for every event's probe key and, unless the Structure was razed, for its <see cref="StructureTimeline.EndOfTurnProbeKey"/>.
    /// </param>
    /// <returns>One entry per coalition that saw at least one event, in coalition id order. Empty when nobody saw anything.</returns>
    /// <exception cref="ArgumentException">A probe has no answer. The witness service answers every probe, so a missing one means the caller built the query wrongly.</exception>
    internal static IReadOnlyList<CoalitionStructureSighting> Fold(StructureTimeline timeline, IReadOnlyDictionary<long, IReadOnlyCollection<WitnessingCoalition>> answersByProbeKey)
    {
        ArgumentNullException.ThrowIfNull(timeline);
        ArgumentNullException.ThrowIfNull(answersByProbeKey);

        // For each coalition: who is in it, and the events it saw with its answer for each, in the timeline's order.
        Dictionary<int, (IReadOnlyCollection<int> MemberPlayerIds, List<(StructureEvent Event, WitnessingCoalition Answer)> SeenEvents)> sightingsByCoalitionId = [];

        foreach (StructureEvent structureEvent in timeline.Events)
        {
            foreach (WitnessingCoalition coalition in AnswerFor(structureEvent.ProbeKey, timeline, answersByProbeKey))
            {
                if (sightingsByCoalitionId.TryGetValue(coalition.CoalitionId, out var sightings) == false)
                {
                    sightings = (coalition.MemberPlayerIds, []);
                    sightingsByCoalitionId[coalition.CoalitionId] = sightings;
                }

                sightings.SeenEvents.Add((structureEvent, coalition));
            }
        }

        // Who could still see the tile as the Turn ended. A razed Structure was not there to be seen, so it is not probed and nobody sees it still standing.
        HashSet<int> coalitionIdsSeeingItAtTheEnd = timeline.IsRazed
            ? []
            : [.. AnswerFor(timeline.EndOfTurnProbeKey, timeline, answersByProbeKey).Select(coalition => coalition.CoalitionId)];

        List<CoalitionStructureSighting> result = [];

        foreach ((int coalitionId, (IReadOnlyCollection<int> memberPlayerIds, List<(StructureEvent Event, WitnessingCoalition Answer)> seenEvents)) in sightingsByCoalitionId.OrderBy(entry => entry.Key))
        {
            StructureEvent lastSeen = seenEvents[^1].Event;
            StructureEvent? firstCaptureSeen = seenEvents.Select(seen => seen.Event).FirstOrDefault(seenEvent => seenEvent.EventType == StructureHistoryEventType.Captured);

            result.Add(new CoalitionStructureSighting
            {
                CoalitionId = coalitionId,
                MemberPlayerIds = memberPlayerIds,
                FoundedInView = WasSeen(seenEvents, StructureHistoryEventType.Founded),
                CompletedInView = WasSeen(seenEvents, StructureHistoryEventType.Completed),
                CapturedInView = firstCaptureSeen is not null,
                RazedInView = WasSeen(seenEvents, StructureHistoryEventType.Razed),
                ProgressedInView = WasSeen(seenEvents, StructureHistoryEventType.Progressed),
                FirstTurnSequence = seenEvents[0].Event.TurnSequence,
                LastTurnSequence = lastSeen.TurnSequence,
                OwnerPlayerId = lastSeen.OwnerPlayerId,
                PreviousOwnerPlayerId = firstCaptureSeen?.PreviousOwnerPlayerId,
                CompletionPercent = lastSeen.CompletionPercent,
                StillVisible = coalitionIdsSeeingItAtTheEnd.Contains(coalitionId),
                Fidelity = seenEvents.Max(seen => seen.Answer.BestFidelity),
                // The witness service lists witnesses nearest first.
                FirstWitness = seenEvents[0].Answer.Witnesses.First()
            });
        }

        return result;
    }

    private static IReadOnlyCollection<WitnessingCoalition> AnswerFor(long probeKey, StructureTimeline timeline, IReadOnlyDictionary<long, IReadOnlyCollection<WitnessingCoalition>> answersByProbeKey)
    {
        if (answersByProbeKey.TryGetValue(probeKey, out IReadOnlyCollection<WitnessingCoalition>? coalitions) == false)
        {
            throw new ArgumentException($"There is no witness answer for probe key {probeKey} of Structure Instance Id {timeline.StructureInstanceId}.", nameof(answersByProbeKey));
        }

        return coalitions;
    }

    private static bool WasSeen(List<(StructureEvent Event, WitnessingCoalition Answer)> seenEvents, StructureHistoryEventType eventType)
    {
        return seenEvents.Any(seen => seen.Event.EventType == eventType);
    }
}
