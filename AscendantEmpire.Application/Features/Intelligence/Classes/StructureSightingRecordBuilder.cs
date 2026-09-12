using AscendantEmpire.Domain.Entities.Games;
using AscendantEmpire.Domain.Enums.Intelligence;

namespace AscendantEmpire.Application.Features.Intelligence.Classes;

/// <summary>
/// Turns what coalitions saw happen to a Structure into the rows the sightings step writes: structure reports for the feed, and known-Structure memories for the map.
/// </summary>
/// <remarks>
/// <para>
/// The Structure counterpart of <see cref="SightingRecordBuilder"/>, and pure mapping in the same way. One coalition-level sighting fans out into one row per member Player.
/// </para>
/// <para>
/// Every row stores the truth - the real Structure, owners and progress - with the fidelity that was earned beside it. Stripping it down is the read projection's job.
/// </para>
/// </remarks>
internal static class StructureSightingRecordBuilder
{
    /// <summary>
    /// Builds a report for every member of every coalition whose sighting raises one. A coalition that only saw construction progress gets none.
    /// </summary>
    internal static IReadOnlyList<PlayerReport> BuildReports(int gameId, int playerTurnId, StructureTimeline timeline, IEnumerable<CoalitionStructureSighting> sightings)
    {
        ArgumentNullException.ThrowIfNull(timeline);
        ArgumentNullException.ThrowIfNull(sightings);

        List<PlayerReport> reports = [];

        foreach (CoalitionStructureSighting sighting in sightings.Where(sighting => sighting.RaisesReport))
        {
            foreach (int recipientPlayerId in sighting.MemberPlayerIds.Order())
            {
                reports.Add(new PlayerReport
                {
                    GameId = gameId,
                    RecipientPlayerId = recipientPlayerId,
                    RaisedDuringPlayerTurnId = playerTurnId,
                    ReportType = PlayerReportType.StructureSighted,
                    SubjectStructureInstanceId = timeline.StructureInstanceId,
                    SubjectStructureId = timeline.StructureId,
                    SubjectOwnerPlayerId = sighting.OwnerPlayerId,
                    SubjectPreviousOwnerPlayerId = sighting.PreviousOwnerPlayerId,
                    SubjectCompletionPercent = sighting.CompletionPercent,
                    // The coalition's fidelity, not the recipient's own, exactly as for a Force.
                    Fidelity = sighting.Fidelity,
                    // A Structure never moves, so its report starts and ends on the one tile and has no path tiles of its own.
                    FirstCampaignMapTileId = timeline.CampaignMapTileId,
                    LastCampaignMapTileId = timeline.CampaignMapTileId,
                    FirstTurnSequence = sighting.FirstTurnSequence,
                    LastTurnSequence = sighting.LastTurnSequence,
                    ExitDirection = null,
                    StillVisible = sighting.StillVisible,
                    // On a Structure report these two mean founded and razed in view; see the entity.
                    AppearedInView = sighting.FoundedInView,
                    DestroyedInView = sighting.RazedInView,
                    CompletedInView = sighting.CompletedInView,
                    CapturedInView = sighting.CapturedInView,
                    TrackOrdinal = 0,
                    FirstWitnessId = sighting.FirstWitness.ObserverId,
                    FirstWitnessRecordType = sighting.FirstWitness.RecordType,
                    FirstWitnessOwnerPlayerId = sighting.FirstWitness.OwnerPlayerId
                });
            }
        }

        return reports;
    }

    /// <summary>
    /// Builds, for every member of every coalition that saw anything happen to the Structure, what they now know of it.
    /// </summary>
    /// <remarks>
    /// These are candidates, not the rows to save: <see cref="Supersedes"/> decides against what a Player already remembers.
    /// </remarks>
    internal static IReadOnlyList<PlayerKnownStructure> BuildKnownStructures(int gameId, int playerTurnId, StructureTimeline timeline, IEnumerable<CoalitionStructureSighting> sightings)
    {
        ArgumentNullException.ThrowIfNull(timeline);
        ArgumentNullException.ThrowIfNull(sightings);

        List<PlayerKnownStructure> knownStructures = [];

        foreach (CoalitionStructureSighting sighting in sightings)
        {
            foreach (int playerId in sighting.MemberPlayerIds.Order())
            {
                knownStructures.Add(new PlayerKnownStructure
                {
                    GameId = gameId,
                    PlayerId = playerId,
                    StructureInstanceId = timeline.StructureInstanceId,
                    StructureId = timeline.StructureId,
                    OwnerPlayerId = sighting.OwnerPlayerId,
                    ObservedCompletionPercent = sighting.CompletionPercent,
                    // A Structure watched being razed is restatused, never forgotten.
                    Status = sighting.Status,
                    Fidelity = sighting.Fidelity,
                    CampaignMapTileId = timeline.CampaignMapTileId,
                    LastSeenPlayerTurnId = playerTurnId,
                    LastSeenTurnSequence = sighting.LastTurnSequence,
                    StillVisible = sighting.StillVisible
                });
            }
        }

        return knownStructures;
    }

    /// <summary>
    /// Whether a new sighting should replace what a Player already knows of a Structure, by the rule in <see cref="SightingRecency"/>.
    /// </summary>
    internal static bool Supersedes(PlayerKnownStructure candidate, PlayerKnownStructure existing)
    {
        ArgumentNullException.ThrowIfNull(candidate);
        ArgumentNullException.ThrowIfNull(existing);

        return SightingRecency.Supersedes(
            candidatePlayerTurnId: candidate.LastSeenPlayerTurnId,
            candidateTurnSequence: candidate.LastSeenTurnSequence,
            existingPlayerTurnId: existing.LastSeenPlayerTurnId,
            existingTurnSequence: existing.LastSeenTurnSequence);
    }

    /// <summary>
    /// Copies everything a sighting says onto an existing known-Structure row, leaving its identity - id, Game, Player and Structure - alone.
    /// </summary>
    internal static void CopySighting(PlayerKnownStructure from, PlayerKnownStructure to)
    {
        ArgumentNullException.ThrowIfNull(from);
        ArgumentNullException.ThrowIfNull(to);

        to.StructureId = from.StructureId;
        to.OwnerPlayerId = from.OwnerPlayerId;
        to.ObservedCompletionPercent = from.ObservedCompletionPercent;
        to.Status = from.Status;
        to.Fidelity = from.Fidelity;
        to.CampaignMapTileId = from.CampaignMapTileId;
        to.LastSeenPlayerTurnId = from.LastSeenPlayerTurnId;
        to.LastSeenTurnSequence = from.LastSeenTurnSequence;
        to.StillVisible = from.StillVisible;
    }
}
