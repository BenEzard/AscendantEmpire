using AscendantEmpire.Application.Features.Intelligence.Classes;
using AscendantEmpire.Application.Features.Maps.CampaignMaps.Classes;
using AscendantEmpire.BackendContracts.CampaignMaps;
using AscendantEmpire.Domain.Entities.Games;
using AscendantEmpire.Domain.Enums;
using AscendantEmpire.Domain.Enums.Battle;
using AscendantEmpire.Domain.Enums.Intelligence;
using AscendantEmpire.Domain.Enums.Maps;

namespace AscendantEmpire.Tests.Features.Intelligence;

/// <summary>
/// Pure unit tests for <see cref="CombatSightingRecordBuilder"/> — no database, because it maps sightings it is handed into rows it does not save.
/// </summary>
[TestFixture]
internal class CombatSightingRecordBuilderTests
{
    private const int _GAME_ID = 1;
    private const int _PLAYER_TURN_ID = 40;
    private const int _BATTLE_ID = 50;
    private const int _CAMPAIGN_MAP_TILE_ID = 14;
    private const int _ATTACKING_PLAYER_ID = 1;

    private static readonly BattleTimeline _TIMELINE = new()
    {
        BattleId = _BATTLE_ID,
        CampaignMapTileId = _CAMPAIGN_MAP_TILE_ID,
        Location = new Coord(4, 1),
        Events = [new EngagementEvent { ProbeKey = 1, EventType = BattleHistoryEventType.Commenced, ParticipantPlayerId = null, ExitReason = null, TroopsLost = null, GroundHeldByPlayerId = null, TurnSequence = 3 }]
    };

    private static CoalitionEngagementSighting Sighting(
        int[] memberPlayerIds,
        bool commencedInView = false,
        bool concludedInView = false,
        bool sideJoinedInView = false,
        bool sideLeftInView = false,
        int? groundHeldByPlayerId = null,
        int? troopsLost = null,
        SightingFidelity fidelity = SightingFidelity.Detail)
    {
        return new CoalitionEngagementSighting
        {
            CoalitionId = memberPlayerIds.Min(),
            MemberPlayerIds = memberPlayerIds,
            CommencedInView = commencedInView,
            ConcludedInView = concludedInView,
            SideJoinedInView = sideJoinedInView,
            SideLeftInView = sideLeftInView,
            FirstTurnSequence = 3,
            LastTurnSequence = 8,
            GroundHeldByPlayerId = groundHeldByPlayerId,
            TroopsLost = troopsLost,
            StillVisible = concludedInView == false,
            Fidelity = fidelity,
            FirstWitness = new Witness { ObserverId = 10, RecordType = RecordType.Structure, OwnerPlayerId = 2, Location = new Coord(0, 0), Distance = 2, Fidelity = fidelity }
        };
    }

    [Test, Description("Vision is shared by coalitions but reports are addressed to Players, so one coalition's sighting becomes a combat report for each member, on the engagement's one tile.")]
    public void ShouldRaiseOneCombatReportPerMemberOfTheWatchingCoalition()
    {
        IReadOnlyList<PlayerReport> reports = CombatSightingRecordBuilder.BuildReports(
            _GAME_ID,
            _PLAYER_TURN_ID,
            _TIMELINE,
            [Sighting([5, 2], commencedInView: true, concludedInView: true, groundHeldByPlayerId: _ATTACKING_PLAYER_ID, troopsLost: 90)]);

        PlayerReport report = reports[0];

        using (Assert.EnterMultipleScope())
        {
            Assert.That(reports.Select(r => r.RecipientPlayerId), Is.EqualTo(new[] { 2, 5 }));
            Assert.That(report.ReportType, Is.EqualTo(PlayerReportType.CombatSighted));
            Assert.That(report.GameId, Is.EqualTo(_GAME_ID));
            Assert.That(report.RaisedDuringPlayerTurnId, Is.EqualTo(_PLAYER_TURN_ID));
            Assert.That(report.SubjectBattleId, Is.EqualTo(_BATTLE_ID));
            Assert.That(report.SubjectGroundHeldByPlayerId, Is.EqualTo(_ATTACKING_PLAYER_ID));
            Assert.That(report.SubjectTroopsLost, Is.EqualTo(90));
            Assert.That(report.FirstCampaignMapTileId, Is.EqualTo(_CAMPAIGN_MAP_TILE_ID));
            Assert.That(report.LastCampaignMapTileId, Is.EqualTo(_CAMPAIGN_MAP_TILE_ID));
            Assert.That(report.FirstTurnSequence, Is.EqualTo(3));
            Assert.That(report.LastTurnSequence, Is.EqualTo(8));
        }
    }

    [Test, Description("Every sighting raises a report: unlike a Structure's construction step, nothing that happens at an engagement is too minor to be worth telling the watcher.")]
    public void ShouldRaiseAReportForEverySightingWithNothingFilteredOut()
    {
        IReadOnlyList<PlayerReport> reports = CombatSightingRecordBuilder.BuildReports(
            _GAME_ID,
            _PLAYER_TURN_ID,
            _TIMELINE,
            [Sighting([2], sideJoinedInView: true), Sighting([3], sideLeftInView: true, troopsLost: 0)]);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(reports.Select(report => report.RecipientPlayerId), Is.EqualTo(new[] { 2, 3 }));
            Assert.That(reports[0].SideJoinedInView, Is.True);
            Assert.That(reports[1].SideLeftInView, Is.True);
        }
    }

    [Test, Description("Commencing and concluding are carried on the shared appeared and destroyed columns, and the Structure family's own flags stay null on a combat report.")]
    public void ShouldMapTheEngagementsEventsOntoTheSharedColumnsAndLeaveTheStructureFlagsNull()
    {
        PlayerReport report = CombatSightingRecordBuilder.BuildReports(
            _GAME_ID,
            _PLAYER_TURN_ID,
            _TIMELINE,
            [Sighting([2], commencedInView: true, concludedInView: true)]).Single();

        using (Assert.EnterMultipleScope())
        {
            Assert.That(report.AppearedInView, Is.True, "The engagement was seen commencing.");
            Assert.That(report.DestroyedInView, Is.True, "The engagement was seen concluding.");
            Assert.That(report.CompletedInView, Is.Null);
            Assert.That(report.CapturedInView, Is.Null);
        }
    }

    [Test, Description("An engagement does not move, so its report has no track, no heading and no Force or Structure subject to be confused with.")]
    public void ShouldLeaveTheMovementAndOtherSubjectColumnsEmpty()
    {
        PlayerReport report = CombatSightingRecordBuilder.BuildReports(_GAME_ID, _PLAYER_TURN_ID, _TIMELINE, [Sighting([2], commencedInView: true)]).Single();

        using (Assert.EnterMultipleScope())
        {
            Assert.That(report.TrackOrdinal, Is.Zero);
            Assert.That(report.ExitDirection, Is.Null);
            Assert.That(report.PathTiles, Is.Empty);
            Assert.That(report.SubjectUnitInstanceId, Is.Null);
            Assert.That(report.SubjectStructureInstanceId, Is.Null);
        }
    }

    [Test, Description("The report carries the coalition's fidelity and its nearest first witness, not the recipient's own, so an ally's better look is what the reader is told from.")]
    public void ShouldCarryTheCoalitionsFidelityAndFirstWitness()
    {
        PlayerReport report = CombatSightingRecordBuilder.BuildReports(
            _GAME_ID,
            _PLAYER_TURN_ID,
            _TIMELINE,
            [Sighting([5, 2], commencedInView: true, fidelity: SightingFidelity.Owner)])[1];

        using (Assert.EnterMultipleScope())
        {
            Assert.That(report.RecipientPlayerId, Is.EqualTo(5));
            Assert.That(report.Fidelity, Is.EqualTo(SightingFidelity.Owner));
            Assert.That(report.FirstWitnessId, Is.EqualTo(10));
            Assert.That(report.FirstWitnessRecordType, Is.EqualTo(RecordType.Structure));
            Assert.That(report.FirstWitnessOwnerPlayerId, Is.EqualTo(2), "The witness belongs to the ally who saw it, not to the reader.");
        }
    }
}
