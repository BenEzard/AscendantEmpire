using AscendantEmpire.Domain.Entities.Games;
using AscendantEmpire.Domain.Enums.Intelligence;

namespace AscendantEmpire.Application.Features.Intelligence.Classes;

/// <summary>
/// Turns what coalitions saw happen at an engagement into the combat reports the sightings step writes.
/// </summary>
/// <remarks>
/// <para>
/// The Battle counterpart of <see cref="SightingRecordBuilder"/> and <see cref="StructureSightingRecordBuilder"/>, and pure mapping in the same way. One coalition-level sighting fans out into one
/// row per member Player.
/// </para>
/// <para>
/// <b>Reports only.</b> There is deliberately no known-object half here, unlike both siblings: an engagement is a point-in-time event rather than a thing standing somewhere, so there is no
/// position for a Player to go on remembering and no marker to age. What a watcher is left holding is the report.
/// </para>
/// <para>
/// Every row stores the truth - the real Battle, the ground-holder and the losses - with the fidelity that was earned beside it. Stripping it down is the read projection's job.
/// </para>
/// </remarks>
internal static class CombatSightingRecordBuilder
{
    /// <summary>
    /// Builds a report for every member of every coalition that saw anything happen at the engagement.
    /// </summary>
    /// <remarks>
    /// Unlike the Structure builder there is nothing to filter out: an engagement has no equivalent of a construction step, so everything witnessed is news.
    /// </remarks>
    internal static IReadOnlyList<PlayerReport> BuildReports(int gameId, int playerTurnId, BattleTimeline timeline, IEnumerable<CoalitionEngagementSighting> sightings)
    {
        ArgumentNullException.ThrowIfNull(timeline);
        ArgumentNullException.ThrowIfNull(sightings);

        List<PlayerReport> reports = [];

        foreach (CoalitionEngagementSighting sighting in sightings)
        {
            foreach (int recipientPlayerId in sighting.MemberPlayerIds.Order())
            {
                reports.Add(new PlayerReport
                {
                    GameId = gameId,
                    RecipientPlayerId = recipientPlayerId,
                    RaisedDuringPlayerTurnId = playerTurnId,
                    ReportType = PlayerReportType.CombatSighted,
                    SubjectBattleId = timeline.BattleId,
                    SubjectGroundHeldByPlayerId = sighting.GroundHeldByPlayerId,
                    SubjectTroopsLost = sighting.TroopsLost,
                    // The coalition's fidelity, not the recipient's own, exactly as for a Force and a Structure.
                    Fidelity = sighting.Fidelity,
                    // An engagement is fought over one tile, so its report starts and ends there and has no path tiles of its own.
                    FirstCampaignMapTileId = timeline.CampaignMapTileId,
                    LastCampaignMapTileId = timeline.CampaignMapTileId,
                    FirstTurnSequence = sighting.FirstTurnSequence,
                    LastTurnSequence = sighting.LastTurnSequence,
                    ExitDirection = null,
                    StillVisible = sighting.StillVisible,
                    // On a combat report these two mean the engagement was seen commencing and concluding; see the entity.
                    AppearedInView = sighting.CommencedInView,
                    DestroyedInView = sighting.ConcludedInView,
                    // The Structure family's flags, which a combat report has no equivalent of.
                    CompletedInView = null,
                    CapturedInView = null,
                    SideJoinedInView = sighting.SideJoinedInView,
                    SideLeftInView = sighting.SideLeftInView,
                    TrackOrdinal = 0,
                    FirstWitnessId = sighting.FirstWitness.ObserverId,
                    FirstWitnessRecordType = sighting.FirstWitness.RecordType,
                    FirstWitnessOwnerPlayerId = sighting.FirstWitness.OwnerPlayerId
                });
            }
        }

        return reports;
    }
}
