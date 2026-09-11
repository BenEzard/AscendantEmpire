using AscendantEmpire.Application.Features.Maps.CampaignMaps.Classes;
using AscendantEmpire.Domain.Enums.Maps;

namespace AscendantEmpire.Application.Features.Intelligence.Classes;

/// <summary>
/// Folds a Force's path and the witness service's answers into tracks: the maximal runs of steps each coalition could see.
/// </summary>
/// <remarks>
/// <para>
/// The pure heart of the sightings step - no database. <c>IWitnessService</c> has already decided which coalitions saw each step; this decides what those sightings amount to.
/// </para>
/// <para>
/// <b>Per coalition, not per observer.</b> Visibility is the union across the coalition's Observers, so three Units watching one road produce one track rather than three. The nearest Observer at
/// the start of the run is kept as the witness that picked the Force up.
/// </para>
/// <para>
/// <b>Capped.</b> A scout circling a watchtower could otherwise flood the feed with tracks. Past <see cref="MAX_TRACKS_PER_FORCE"/>, the first ones and the last one are kept, so the final exit or
/// still-visible state always survives.
/// </para>
/// </remarks>
internal static class ForceTrackFolder
{
    /// <summary>
    /// The most tracks one coalition is told about for one Force in one Turn.
    /// </summary>
    /// <remarks>
    /// The design's starting figure. The first <c>MAX_TRACKS_PER_FORCE - 1</c> tracks and the last are kept; the ones in between are dropped.
    /// </remarks>
    internal const int MAX_TRACKS_PER_FORCE = 3;

    /// <summary>
    /// Works out every coalition's tracks of one Force.
    /// </summary>
    /// <param name="answersByProbeKey">The witness service's answers, which must hold an entry for every step's probe key.</param>
    /// <returns>One entry per coalition that saw the Force at all, in coalition id order. Empty when nobody saw it.</returns>
    /// <exception cref="ArgumentException">A step has no answer. The witness service answers every probe, so a missing one means the caller built the query wrongly.</exception>
    internal static IReadOnlyList<CoalitionForceTracks> Fold(ForcePath path, IReadOnlyDictionary<long, IReadOnlyCollection<WitnessingCoalition>> answersByProbeKey, CampaignMapGeometry geometry)
    {
        ArgumentNullException.ThrowIfNull(path);
        ArgumentNullException.ThrowIfNull(answersByProbeKey);
        ArgumentNullException.ThrowIfNull(geometry);

        // For each coalition: who is in it, and which of the Force's steps it saw, keyed by step index so the runs can be read off in order.
        Dictionary<int, (IReadOnlyCollection<int> MemberPlayerIds, SortedDictionary<int, WitnessingCoalition> SeenSteps)> sightingsByCoalitionId = [];

        for (int stepIndex = 0; stepIndex < path.Steps.Count; stepIndex++)
        {
            ForceStep step = path.Steps[stepIndex];

            if (answersByProbeKey.TryGetValue(step.ProbeKey, out IReadOnlyCollection<WitnessingCoalition>? coalitions) == false)
            {
                throw new ArgumentException($"There is no witness answer for probe key {step.ProbeKey} of Unit Instance Id {path.UnitInstanceId}.", nameof(answersByProbeKey));
            }

            foreach (WitnessingCoalition coalition in coalitions)
            {
                if (sightingsByCoalitionId.TryGetValue(coalition.CoalitionId, out var sightings) == false)
                {
                    sightings = (coalition.MemberPlayerIds, new SortedDictionary<int, WitnessingCoalition>());
                    sightingsByCoalitionId[coalition.CoalitionId] = sightings;
                }

                sightings.SeenSteps[stepIndex] = coalition;
            }
        }

        List<CoalitionForceTracks> result = [];

        foreach ((int coalitionId, (IReadOnlyCollection<int> memberPlayerIds, SortedDictionary<int, WitnessingCoalition> seenSteps)) in sightingsByCoalitionId.OrderBy(entry => entry.Key))
        {
            List<ForceTrack> tracks = [.. SplitIntoRuns(seenSteps.Keys).Select(run => BuildTrack(path, run, seenSteps, geometry))];

            IReadOnlyList<ForceTrack> keptTracks = ApplyCap(tracks);

            result.Add(new CoalitionForceTracks
            {
                CoalitionId = coalitionId,
                MemberPlayerIds = memberPlayerIds,
                // Numbered after the cap, so the ordinals a recipient sees are always 0, 1, 2 with no gaps where a dropped track was.
                Tracks = [.. keptTracks.Select((track, ordinal) => track with { TrackOrdinal = ordinal })]
            });
        }

        return result;
    }

    /// <summary>
    /// Splits ascending step indices into runs of consecutive indices.
    /// </summary>
    private static List<List<int>> SplitIntoRuns(IEnumerable<int> ascendingStepIndices)
    {
        List<List<int>> runs = [];

        foreach (int stepIndex in ascendingStepIndices)
        {
            // A gap of even one unseen step ends the run: the coalition lost sight of the Force, so whatever it sees next is a new track.
            if (runs.Count == 0 || runs[^1][^1] != stepIndex - 1)
            {
                runs.Add([]);
            }

            runs[^1].Add(stepIndex);
        }

        return runs;
    }

    private static ForceTrack BuildTrack(ForcePath path, List<int> run, SortedDictionary<int, WitnessingCoalition> seenSteps, CampaignMapGeometry geometry)
    {
        ForceStep firstStep = path.Steps[run[0]];
        int lastStepIndex = run[^1];
        ForceStep lastStep = path.Steps[lastStepIndex];

        // The step after the run, if there is one, is the one the coalition did not see.
        ForceStep? stepAfterRun = lastStepIndex + 1 < path.Steps.Count ? path.Steps[lastStepIndex + 1] : null;

        // Only a move out of view has a direction. A Force destroyed just out of sight did not go anywhere, and one still in view at the end has not left. A pair of tiles that are not neighbours
        // cannot be named a direction, and reads as none rather than stopping the Turn from ending.
        MoveDirection? exitDirection = null;

        if (stepAfterRun is { Kind: ForceStepKind.Moved } && geometry.TryGetStepDirection(lastStep.Location, stepAfterRun.Location, out MoveDirection direction))
        {
            exitDirection = direction;
        }

        List<int> pathTileIds = [];

        foreach (int stepIndex in run)
        {
            int tileId = path.Steps[stepIndex].CampaignMapTileId;

            if (pathTileIds.Count == 0 || pathTileIds[^1] != tileId)
            {
                pathTileIds.Add(tileId);
            }
        }

        return new ForceTrack
        {
            // Placeholder: numbered once the cap has decided which tracks survive.
            TrackOrdinal = 0,
            PathTileIds = pathTileIds,
            FirstTurnSequence = firstStep.TurnSequence,
            LastTurnSequence = lastStep.TurnSequence,
            ExitDirection = exitDirection,
            StillVisible = stepAfterRun is null && lastStep.Kind != ForceStepKind.Destroyed,
            AppearedInView = firstStep.Kind == ForceStepKind.Created,
            DestroyedInView = lastStep.Kind == ForceStepKind.Destroyed,
            Fidelity = run.Max(stepIndex => seenSteps[stepIndex].BestFidelity),
            // The witness service lists witnesses nearest first.
            FirstWitness = seenSteps[run[0]].Witnesses.First()
        };
    }

    private static IReadOnlyList<ForceTrack> ApplyCap(List<ForceTrack> tracks)
    {
        if (tracks.Count <= MAX_TRACKS_PER_FORCE)
        {
            return tracks;
        }

        // The first few say where the Force was first seen; the last says where it was last seen and which way it went, which the known-Force memory and the feed both need to be true.
        return [.. tracks.Take(MAX_TRACKS_PER_FORCE - 1), tracks[^1]];
    }
}
