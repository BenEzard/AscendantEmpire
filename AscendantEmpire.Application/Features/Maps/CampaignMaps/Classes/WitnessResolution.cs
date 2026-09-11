using AscendantEmpire.Domain.Enums.Maps;

namespace AscendantEmpire.Application.Features.Maps.CampaignMaps.Classes;

/// <summary>
/// The pure heart of the inverted sight question: given the fields of view near some probed tiles, which coalitions saw each one, and how well.
/// </summary>
/// <remarks>
/// <para>
/// No database. <c>IWitnessService</c> works out which Observers are close enough to matter and computes their fields of view through the same
/// <see cref="FieldOfView"/> the live map read uses; this decides what those views mean for each probe.
/// </para>
/// <para>
/// An Observer is a witness to a probe when:
/// <list type="bullet">
///   <item><description>its owner belongs to one of the observing coalitions passed in — anything else, including the acting side, is ignored;</description></item>
///   <item><description>its field of view includes the probed tile; and</description></item>
///   <item><description>it had not yet been destroyed at the probed sequence.</description></item>
/// </list>
/// </para>
/// </remarks>
internal static class WitnessResolution
{
    /// <summary>
    /// Answers every probe, keyed by <see cref="WitnessProbe.Key"/>.
    /// </summary>
    /// <param name="fieldsOfView">The views of every Observer that might matter. Each is computed once and reused for every probe it covers.</param>
    /// <param name="observingCoalitions">The coalitions that may witness these events, which are disjoint.</param>
    /// <param name="visibilityByTemplateId">Each probed Force's <c>TotalVisibility</c>, keyed by template. A template missing here reads as zero.</param>
    /// <returns>An entry for every probe, empty when nobody saw it. Coalitions are in id order.</returns>
    /// <exception cref="ArgumentException">Two probes share a key.</exception>
    internal static IReadOnlyDictionary<long, IReadOnlyCollection<WitnessingCoalition>> Resolve(
        int playerTurnId,
        IReadOnlyCollection<WitnessProbe> probes,
        IReadOnlyCollection<ObserverFieldOfView> fieldsOfView,
        IReadOnlyCollection<VisionCoalition> observingCoalitions,
        IReadOnlyDictionary<int, long> visibilityByTemplateId,
        CampaignMapGeometry geometry)
    {
        ArgumentNullException.ThrowIfNull(probes);
        ArgumentNullException.ThrowIfNull(fieldsOfView);
        ArgumentNullException.ThrowIfNull(observingCoalitions);
        ArgumentNullException.ThrowIfNull(visibilityByTemplateId);
        ArgumentNullException.ThrowIfNull(geometry);

        Dictionary<int, VisionCoalition> coalitionByPlayerId = [];

        foreach (VisionCoalition coalition in observingCoalitions)
        {
            foreach (int memberPlayerId in coalition.MemberPlayerIds)
            {
                coalitionByPlayerId[memberPlayerId] = coalition;
            }
        }

        // Index the views by the tiles they cover, so a probe consults only the Observers that could possibly have seen its tile rather than all of them. An
        // Observer outside every observing coalition is dropped here, once, which is what keeps the acting side from ever witnessing itself.
        Dictionary<(int X, int Y), List<(ObserverFieldOfView View, SightTerrain Tile, VisionCoalition Coalition)>> watchersByCoord = [];

        foreach (ObserverFieldOfView view in fieldsOfView)
        {
            if (coalitionByPlayerId.TryGetValue(view.Observer.PlayerId, out VisionCoalition? coalition) == false)
            {
                continue;
            }

            foreach (SightTerrain tile in view.SeenTiles)
            {
                if (watchersByCoord.TryGetValue((tile.X, tile.Y), out var watchers) == false)
                {
                    watchers = [];
                    watchersByCoord[(tile.X, tile.Y)] = watchers;
                }

                watchers.Add((view, tile, coalition));
            }
        }

        Dictionary<long, IReadOnlyCollection<WitnessingCoalition>> answers = [];

        foreach (WitnessProbe probe in probes)
        {
            if (answers.ContainsKey(probe.Key))
            {
                throw new ArgumentException($"Probe key {probe.Key} is used more than once; each probe needs its own key for its answer to be found.", nameof(probes));
            }

            List<(VisionCoalition Coalition, Witness Witness)> witnesses = [];

            if (watchersByCoord.TryGetValue((probe.Location.X, probe.Location.Y), out var watchersOfTile))
            {
                foreach ((ObserverFieldOfView view, SightTerrain tile, VisionCoalition coalition) in watchersOfTile)
                {
                    SightObserver observer = view.Observer;

                    // Destroyed at this sequence or earlier: it was not there to see. Strictly before counts, so a tower that saw the cavalry at 3 and fell at
                    // 9 still reports, while whatever happened at 9 itself - including its own destruction - it did not live to see.
                    if (observer.DestroyedAtSequence is int destroyedAtSequence && probe.TurnSequence >= destroyedAtSequence)
                    {
                        continue;
                    }

                    int distance = geometry.GetDistance(observer.Location, probe.Location);

                    witnesses.Add((coalition, new Witness
                    {
                        ObserverId = observer.Id,
                        RecordType = observer.RecordType,
                        OwnerPlayerId = observer.PlayerId,
                        Location = observer.Location,
                        Distance = distance,
                        Fidelity = ResolveFidelity(playerTurnId, probe, observer, tile, distance, visibilityByTemplateId)
                    }));
                }
            }

            answers[probe.Key] =
            [
                .. witnesses
                    .GroupBy(entry => entry.Coalition.CoalitionId)
                    .OrderBy(group => group.Key)
                    .Select(group => new WitnessingCoalition
                    {
                        CoalitionId = group.Key,
                        MemberPlayerIds = group.First().Coalition.MemberPlayerIds,
                        // Nearest first, then a stable tie-break, so the same Turn always folds into the same answer.
                        Witnesses =
                        [
                            .. group
                                .Select(entry => entry.Witness)
                                .OrderBy(witness => witness.Distance)
                                .ThenBy(witness => witness.RecordType)
                                .ThenBy(witness => witness.ObserverId)
                        ]
                    })
            ];
        }

        return answers;
    }

    private static SightingFidelity ResolveFidelity(
        int playerTurnId,
        WitnessProbe probe,
        SightObserver observer,
        SightTerrain tile,
        int distance,
        IReadOnlyDictionary<int, long> visibilityByTemplateId)
    {
        if (probe.Force is not ForceSubject force)
        {
            return SightingFidelityRules.ForTile();
        }

        return SightingFidelityRules.ForForce(new ForceSightingInputs
        {
            PlayerTurnId = playerTurnId,
            TurnSequence = probe.TurnSequence,
            ObserverId = observer.Id,
            ObserverRecordType = observer.RecordType,
            ObserverDetection = observer.Detection,
            ObserverHealthyTroops = observer.HealthyTroops,
            Distance = distance,
            SubjectUnitInstanceId = force.UnitInstanceId,
            SubjectUnitTemplateId = force.UnitTemplateId,
            SubjectVisibility = visibilityByTemplateId.GetValueOrDefault(force.UnitTemplateId),
            SubjectHealthyTroops = force.HealthyTroops,
            SubjectBiomeType = tile.BiomeType,
            SubjectGeographyType = tile.GeographyType
        });
    }
}
