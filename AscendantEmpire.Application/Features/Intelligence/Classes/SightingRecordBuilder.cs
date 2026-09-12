using AscendantEmpire.Domain.Entities.Games;
using AscendantEmpire.Domain.Enums.Intelligence;

namespace AscendantEmpire.Application.Features.Intelligence.Classes;

/// <summary>
/// Turns coalition tracks into the rows a sightings step writes: reports for the feed, and known-Force memories for the map.
/// </summary>
/// <remarks>
/// <para>
/// Pure mapping - no database. The fan-out happens here: one coalition-level track becomes one report per member Player, because reports are addressed to Players while vision is shared by
/// coalitions.
/// </para>
/// <para>
/// Every row stores the truth - the real Force, owner, template and troops - with the fidelity that was earned beside it. Stripping identity down to that fidelity is the read projection's job.
/// </para>
/// </remarks>
internal static class SightingRecordBuilder
{
    /// <summary>
    /// Builds a report for every track, for every member of the coalition that saw it.
    /// </summary>
    /// <param name="healthyTroops">The Force's unwounded soldiers now, or null when it no longer exists.</param>
    internal static IReadOnlyList<PlayerReport> BuildReports(int gameId, int playerTurnId, ForcePath path, IEnumerable<CoalitionForceTracks> coalitionTracks, int? healthyTroops)
    {
        ArgumentNullException.ThrowIfNull(path);
        ArgumentNullException.ThrowIfNull(coalitionTracks);

        List<PlayerReport> reports = [];

        foreach (CoalitionForceTracks coalition in coalitionTracks)
        {
            foreach (int recipientPlayerId in coalition.MemberPlayerIds.Order())
            {
                foreach (ForceTrack track in coalition.Tracks)
                {
                    reports.Add(new PlayerReport
                    {
                        GameId = gameId,
                        RecipientPlayerId = recipientPlayerId,
                        RaisedDuringPlayerTurnId = playerTurnId,
                        ReportType = PlayerReportType.ForceSighted,
                        SubjectUnitInstanceId = path.UnitInstanceId,
                        SubjectOwnerPlayerId = path.OwnerPlayerId,
                        SubjectUnitTemplateId = path.UnitTemplateId,
                        SubjectHealthyTroops = healthyTroops,
                        // The coalition's fidelity, not the recipient's own: under shared vision an ally with a good scout sees more than you would have.
                        Fidelity = track.Fidelity,
                        FirstCampaignMapTileId = track.FirstCampaignMapTileId,
                        LastCampaignMapTileId = track.LastCampaignMapTileId,
                        FirstTurnSequence = track.FirstTurnSequence,
                        LastTurnSequence = track.LastTurnSequence,
                        ExitDirection = track.ExitDirection,
                        StillVisible = track.StillVisible,
                        AppearedInView = track.AppearedInView,
                        DestroyedInView = track.DestroyedInView,
                        TrackOrdinal = track.TrackOrdinal,
                        FirstWitnessId = track.FirstWitness.ObserverId,
                        FirstWitnessRecordType = track.FirstWitness.RecordType,
                        FirstWitnessOwnerPlayerId = track.FirstWitness.OwnerPlayerId,
                        PathTiles = [.. track.PathTileIds.Select((tileId, ordinal) => new PlayerReportPathTile { Ordinal = ordinal, CampaignMapTileId = tileId })]
                    });
                }
            }
        }

        return reports;
    }

    /// <summary>
    /// Builds, for every member of every coalition that saw the Force, what they now know of it: its latest track.
    /// </summary>
    /// <remarks>
    /// These are candidates, not the rows to save. A Player may already know about the Force from a later sighting - a re-run of an older Turn, say - and <see cref="Supersedes"/> decides.
    /// </remarks>
    /// <param name="healthyTroops">The Force's unwounded soldiers now, or null when it no longer exists.</param>
    internal static IReadOnlyList<PlayerKnownForce> BuildKnownForces(int gameId, int playerTurnId, ForcePath path, IEnumerable<CoalitionForceTracks> coalitionTracks, int? healthyTroops)
    {
        ArgumentNullException.ThrowIfNull(path);
        ArgumentNullException.ThrowIfNull(coalitionTracks);

        List<PlayerKnownForce> knownForces = [];

        foreach (CoalitionForceTracks coalition in coalitionTracks)
        {
            ForceTrack latestTrack = coalition.LatestTrack;

            foreach (int playerId in coalition.MemberPlayerIds.Order())
            {
                knownForces.Add(new PlayerKnownForce
                {
                    GameId = gameId,
                    PlayerId = playerId,
                    UnitInstanceId = path.UnitInstanceId,
                    OwnerPlayerId = path.OwnerPlayerId,
                    UnitTemplateId = path.UnitTemplateId,
                    HealthyTroops = healthyTroops,
                    // A Force watched dying is restatused, never forgotten.
                    Status = latestTrack.DestroyedInView ? KnownForceStatus.Destroyed : KnownForceStatus.Sighted,
                    Fidelity = latestTrack.Fidelity,
                    LastSeenCampaignMapTileId = latestTrack.LastCampaignMapTileId,
                    LastSeenPlayerTurnId = playerTurnId,
                    LastSeenTurnSequence = latestTrack.LastTurnSequence,
                    ExitDirection = latestTrack.ExitDirection,
                    StillVisible = latestTrack.StillVisible
                });
            }
        }

        return knownForces;
    }

    /// <summary>
    /// Whether a new sighting should replace what a Player already knows of a Force.
    /// </summary>
    /// <remarks>
    /// The rule itself is <see cref="SightingRecency"/>, shared with known Structures so the two memories can never disagree about what "more recent" means.
    /// </remarks>
    internal static bool Supersedes(PlayerKnownForce candidate, PlayerKnownForce existing)
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
    /// Copies everything a sighting says onto an existing known-Force row, leaving its identity - id, Game, Player and Force - alone.
    /// </summary>
    internal static void CopySighting(PlayerKnownForce from, PlayerKnownForce to)
    {
        ArgumentNullException.ThrowIfNull(from);
        ArgumentNullException.ThrowIfNull(to);

        to.OwnerPlayerId = from.OwnerPlayerId;
        to.UnitTemplateId = from.UnitTemplateId;
        to.HealthyTroops = from.HealthyTroops;
        to.Status = from.Status;
        to.Fidelity = from.Fidelity;
        to.LastSeenCampaignMapTileId = from.LastSeenCampaignMapTileId;
        to.LastSeenPlayerTurnId = from.LastSeenPlayerTurnId;
        to.LastSeenTurnSequence = from.LastSeenTurnSequence;
        to.ExitDirection = from.ExitDirection;
        to.StillVisible = from.StillVisible;
    }
}
