using AscendantEmpire.Application.Common;
using AscendantEmpire.Application.Data;
using AscendantEmpire.Application.Features.Game.Handlers;
using AscendantEmpire.Application.Features.Intelligence.Services;
using AscendantEmpire.Application.Features.Maps.CampaignMaps.Services;
using AscendantEmpire.Domain.Entities.Battles;
using AscendantEmpire.Domain.Entities.Games;
using AscendantEmpire.Domain.Entities.Units;
using AscendantEmpire.Domain.Enums.Battle;
using AscendantEmpire.Domain.Enums.Forces;
using AscendantEmpire.Domain.Enums.Intelligence;
using AscendantEmpire.Tests.Features.Maps.CampaignMaps;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;

using static AscendantEmpire.Application.Common.ServiceResultBase;

namespace AscendantEmpire.Tests.Features.Intelligence;

/// <summary>
/// End-to-end tests of <see cref="CombatSightingFoldService"/> over a real Game: battle history in, combat reports out.
/// </summary>
/// <remarks>
/// <para>
/// On the containerised tier, for the same reasons as <see cref="StructureSightingFoldServiceTests"/>: the step replaces a Turn's reports with <c>ExecuteDeleteAsync</c>, inside an explicit
/// transaction. The witness service and the sight rules underneath it are the real ones, so a report here means the fighting was genuinely in view.
/// </para>
/// <para>
/// The map is 10 x 3 of Flat Grassland. Three Players: the acting one, a watcher whose starting Forces are all moved to (8, 1) so they see columns 5 to 9 and nothing west of that, and a third
/// who is the acting Player's opponent in the fighting and whose Forces stand right beside it. Battles and their history are written straight to the tables: this fixture is about what the step
/// makes of a history, not how the history was written.
/// </para>
/// </remarks>
[TestFixture]
internal class CombatSightingFoldServiceTests : GameSqlServerTestBase
{
    private const int _MAP_WIDTH = 10;
    private const int _MAP_HEIGHT = 3;

    /// <summary>Well inside the watcher's view from (8, 1).</summary>
    private const int _WATCHED_X = 6;

    /// <summary>The far west of the map, out of the watcher's view and out of the opponent's.</summary>
    private const int _UNWATCHED_X = 1;

    private const int _BATTLE_ROW = 1;

    private CombatSightingFoldService _combatFoldService = null!;
    private ForceSightingFoldService _forceFoldService = null!;

    private int _gameId;
    private int _campaignMapOffsetId;
    private int _actingPlayerId;
    private int _watchingPlayerId;
    private int _opposingPlayerId;
    private int _settlerId;
    private int _settlerTemplateId;
    private int _playerTurnId;

    [SetUp]
    public async Task SetUpGameAsync()
    {
        await SeedGameAsync(mapWidth: _MAP_WIDTH, mapHeight: _MAP_HEIGHT, numberOfPlayers: 3);

        ServiceResult<int> setupResult = await _service.SetupGameAsync(
            new SetupGameRequest(GalleryMapId: _galleryIndexId, GameName: "Watched Combat Game", NumberOfPlayers: 3, BattleMapSize: 50, GenerationSeed: null),
            CancellationToken.None);

        Assert.That(setupResult.HasSucceeded, Is.True, () => $"{string.Join("; ", setupResult.Errors)} Logged exception: {_capturingLogger.LastException}");

        _gameId = setupResult.SuccessValue;

        await using (AscendantEmpireContext context = await DbFactory.CreateDbContextAsync())
        {
            _campaignMapOffsetId = await context.GameIndex.Where(g => g.Id == _gameId).Select(g => g.CampaignMapOffsetId).SingleAsync();

            int[] playerIds = await context.Players.Where(p => p.GameId == _gameId).OrderBy(p => p.Id).Select(p => p.Id).ToArrayAsync();
            _actingPlayerId = playerIds[0];
            _watchingPlayerId = playerIds[1];
            _opposingPlayerId = playerIds[2];

            _settlerTemplateId = await context.UnitTemplates.Where(t => t.Code == "SETTLER").Select(t => t.Id).SingleAsync();
            _settlerId = await context.UnitInstances
                .Where(u => u.GameId == _gameId && u.PlayerId == _actingPlayerId && u.UnitTemplateId == _settlerTemplateId)
                .Select(u => u.Id)
                .FirstAsync();

            // Every one of the watcher's starting Forces onto one tile, so what they can see is fixed and easy to reason about.
            await context.UnitInstances
                .Where(u => u.GameId == _gameId && u.PlayerId == _watchingPlayerId)
                .ExecuteUpdateAsync(setters => setters.SetProperty(u => u.CampaignMapTileId, TileId(8, _BATTLE_ROW)));

            // The opponent stands right beside the watched tile, so if participants were not excluded they would certainly witness their own battle.
            await context.UnitInstances
                .Where(u => u.GameId == _gameId && u.PlayerId == _opposingPlayerId)
                .ExecuteUpdateAsync(setters => setters.SetProperty(u => u.CampaignMapTileId, TileId(_WATCHED_X, _BATTLE_ROW)));
        }

        // The acting Player is the lowest id, so this opens their Turn.
        _playerTurnId = await OpenFirstPlayerTurnAsync(_gameId);

        var sightDataReader = new SightDataReader();
        var fieldOfViewService = new FieldOfViewService(NullLogger<FieldOfViewService>.Instance, DbFactory, sightDataReader, SightTestData.AllStrategies);
        var witnessService = new WitnessService(NullLogger<WitnessService>.Instance, DbFactory, sightDataReader, new SoloCoalitionResolver(), fieldOfViewService);

        _combatFoldService = new CombatSightingFoldService(NullLogger<CombatSightingFoldService>.Instance, DbFactory, sightDataReader, witnessService);
        _forceFoldService = new ForceSightingFoldService(NullLogger<ForceSightingFoldService>.Instance, DbFactory, sightDataReader, witnessService);
    }

    [Test, Description("Fighting between two other Players on ground a third watches is one report to that third Player, saying it broke out, ended, and who was left holding the ground.")]
    public async Task ShouldRaiseOneReportForAnEngagementFoughtAndFinishedInViewAsync()
    {
        int battleId = await SeedBattleAsync(_WATCHED_X, concluded: true);

        await SeedHistoryAsync(
            battleId,
            _WATCHED_X,
            (BattleHistoryEventType.Commenced, null, null, null, Sequence: 3),
            (BattleHistoryEventType.ParticipantLeft, _opposingPlayerId, TroopsLost: 120, null, Sequence: 8),
            (BattleHistoryEventType.Concluded, null, null, GroundHeldBy: _actingPlayerId, Sequence: 9));

        await FoldCombatAsync();

        await using AscendantEmpireContext context = await DbFactory.CreateDbContextAsync();

        PlayerReport report = await context.PlayerReports.SingleAsync(r => r.GameId == _gameId);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(report.RecipientPlayerId, Is.EqualTo(_watchingPlayerId));
            Assert.That(report.ReportType, Is.EqualTo(PlayerReportType.CombatSighted));
            Assert.That(report.SubjectBattleId, Is.EqualTo(battleId));
            Assert.That(report.AppearedInView, Is.True, "The engagement was seen commencing.");
            Assert.That(report.DestroyedInView, Is.True, "The engagement was seen concluding.");
            Assert.That(report.SideLeftInView, Is.True);
            Assert.That(report.SideJoinedInView, Is.False);
            Assert.That(report.StillVisible, Is.False, "A concluded engagement is not still being fought.");
            Assert.That(report.SubjectGroundHeldByPlayerId, Is.EqualTo(_actingPlayerId));
            Assert.That(report.SubjectTroopsLost, Is.EqualTo(120));
            Assert.That(report.FirstCampaignMapTileId, Is.EqualTo(TileId(_WATCHED_X, _BATTLE_ROW)));
            Assert.That(report.LastCampaignMapTileId, Is.EqualTo(TileId(_WATCHED_X, _BATTLE_ROW)));
            Assert.That((report.FirstTurnSequence, report.LastTurnSequence), Is.EqualTo((3, 9)));
            Assert.That(report.CompletedInView, Is.Null, "The Structure family's flags stay null on a combat report.");
            Assert.That(report.CapturedInView, Is.Null);
        }
    }

    [Test, Description("A side in the fighting is never told about its own battle, even standing next to it and even when the Turn is somebody else's - a combatant is an actor, not a witness.")]
    public async Task ShouldRaiseNoReportForEitherSideInTheEngagementAsync()
    {
        int battleId = await SeedBattleAsync(_WATCHED_X, concluded: true);

        await SeedHistoryAsync(
            battleId,
            _WATCHED_X,
            (BattleHistoryEventType.Commenced, null, null, null, Sequence: 3),
            (BattleHistoryEventType.Concluded, null, null, GroundHeldBy: _actingPlayerId, Sequence: 9));

        await FoldCombatAsync();

        await using AscendantEmpireContext context = await DbFactory.CreateDbContextAsync();

        int[] recipientPlayerIds = await context.PlayerReports.Where(r => r.GameId == _gameId).Select(r => r.RecipientPlayerId).ToArrayAsync();

        using (Assert.EnterMultipleScope())
        {
            Assert.That(recipientPlayerIds, Is.EqualTo(new[] { _watchingPlayerId }));
            Assert.That(recipientPlayerIds, Does.Not.Contain(_actingPlayerId), "The acting Player watched it on their own screen.");
            Assert.That(recipientPlayerIds, Does.Not.Contain(_opposingPlayerId), "The opponent is standing in it, and is not the acting Player either.");
        }
    }

    [Test, Description("Fighting out of everybody's view is reported to nobody, because the feed is what was witnessed rather than what happened.")]
    public async Task ShouldRaiseNoReportForAnEngagementNobodyCouldSeeAsync()
    {
        int battleId = await SeedBattleAsync(_UNWATCHED_X, concluded: true);

        await SeedHistoryAsync(
            battleId,
            _UNWATCHED_X,
            (BattleHistoryEventType.Commenced, null, null, null, Sequence: 3),
            (BattleHistoryEventType.Concluded, null, null, GroundHeldBy: _actingPlayerId, Sequence: 9));

        await FoldCombatAsync();

        await using AscendantEmpireContext context = await DbFactory.CreateDbContextAsync();

        Assert.That(await context.PlayerReports.AnyAsync(r => r.GameId == _gameId), Is.False);
    }

    [Test, Description("An engagement still being fought in view as the Turn ends is still visible, which is what lets the feed say the fighting was still going on.")]
    public async Task ShouldMarkAnUnconcludedEngagementStillVisibleAsync()
    {
        int battleId = await SeedBattleAsync(_WATCHED_X, concluded: false);

        await SeedHistoryAsync(
            battleId,
            _WATCHED_X,
            (BattleHistoryEventType.Commenced, null, null, null, Sequence: 3),
            (BattleHistoryEventType.ParticipantJoined, _opposingPlayerId, null, null, Sequence: 4));

        await FoldCombatAsync();

        await using AscendantEmpireContext context = await DbFactory.CreateDbContextAsync();

        PlayerReport report = await context.PlayerReports.SingleAsync(r => r.GameId == _gameId);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(report.StillVisible, Is.True);
            Assert.That(report.DestroyedInView, Is.False);
            Assert.That(report.SideJoinedInView, Is.True);
            Assert.That(report.SubjectTroopsLost, Is.Null, "Nobody was seen leaving, so there are no losses to report.");
            Assert.That(report.SubjectGroundHeldByPlayerId, Is.Null);
        }
    }

    [Test, Description("Losses reach the report only from the departures the watcher saw, so a side that broke after the watcher lost sight of the field takes its losses with it.")]
    public async Task ShouldReportOnlyTheLossesOfTheDeparturesWitnessedAsync()
    {
        int watchedBattleId = await SeedBattleAsync(_WATCHED_X, concluded: true);
        int unwatchedBattleId = await SeedBattleAsync(_UNWATCHED_X, concluded: true);

        await SeedHistoryAsync(
            watchedBattleId,
            _WATCHED_X,
            (BattleHistoryEventType.ParticipantLeft, _opposingPlayerId, TroopsLost: 120, null, Sequence: 3),
            (BattleHistoryEventType.Concluded, null, null, GroundHeldBy: _actingPlayerId, Sequence: 4));

        // The same Turn, but where nobody can see it: its losses must not find their way onto the watched engagement's report.
        await SeedHistoryAsync(
            unwatchedBattleId,
            _UNWATCHED_X,
            (BattleHistoryEventType.ParticipantLeft, _opposingPlayerId, TroopsLost: 900, null, Sequence: 5),
            (BattleHistoryEventType.Concluded, null, null, GroundHeldBy: _actingPlayerId, Sequence: 6));

        await FoldCombatAsync();

        await using AscendantEmpireContext context = await DbFactory.CreateDbContextAsync();

        PlayerReport report = await context.PlayerReports.SingleAsync(r => r.GameId == _gameId);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(report.SubjectBattleId, Is.EqualTo(watchedBattleId));
            Assert.That(report.SubjectTroopsLost, Is.EqualTo(120));
        }
    }

    [Test, Description("Re-running a Turn replaces its combat reports rather than adding to them, so a retried end of Turn cannot double the feed.")]
    public async Task ShouldReplaceTheTurnsCombatReportsOnAReRunAsync()
    {
        int battleId = await SeedBattleAsync(_WATCHED_X, concluded: true);

        await SeedHistoryAsync(
            battleId,
            _WATCHED_X,
            (BattleHistoryEventType.Commenced, null, null, null, Sequence: 3),
            (BattleHistoryEventType.Concluded, null, null, GroundHeldBy: _actingPlayerId, Sequence: 9));

        await FoldCombatAsync();
        await FoldCombatAsync();

        await using AscendantEmpireContext context = await DbFactory.CreateDbContextAsync();

        Assert.That(await context.PlayerReports.CountAsync(r => r.GameId == _gameId), Is.EqualTo(1));
    }

    [Test, Description("The combat step replaces only its own report type, so the Force reports already raised for the same Turn survive it untouched.")]
    public async Task ShouldLeaveTheTurnsForceReportsAloneAsync()
    {
        await SeedMovementAsync(_settlerId, ((5, _BATTLE_ROW), (6, _BATTLE_ROW), Sequence: 1));

        int battleId = await SeedBattleAsync(_WATCHED_X, concluded: true);

        await SeedHistoryAsync(
            battleId,
            _WATCHED_X,
            (BattleHistoryEventType.Commenced, null, null, null, Sequence: 3),
            (BattleHistoryEventType.Concluded, null, null, GroundHeldBy: _actingPlayerId, Sequence: 9));

        long[] forceReportIdsBefore;

        await FoldForcesAsync();

        await using (AscendantEmpireContext beforeContext = await DbFactory.CreateDbContextAsync())
        {
            forceReportIdsBefore = await beforeContext.PlayerReports
                .Where(r => r.GameId == _gameId && r.ReportType == PlayerReportType.ForceSighted)
                .Select(r => r.Id)
                .OrderBy(id => id)
                .ToArrayAsync();

            Assert.That(forceReportIdsBefore, Is.Not.Empty, "The Force step has to have raised something for this test to be about anything.");
        }

        await FoldCombatAsync();

        await using AscendantEmpireContext context = await DbFactory.CreateDbContextAsync();

        long[] forceReportIdsAfter = await context.PlayerReports
            .Where(r => r.GameId == _gameId && r.ReportType == PlayerReportType.ForceSighted)
            .Select(r => r.Id)
            .OrderBy(id => id)
            .ToArrayAsync();

        using (Assert.EnterMultipleScope())
        {
            Assert.That(forceReportIdsAfter, Is.EqualTo(forceReportIdsBefore), "The combat step deletes only its own report type, so the Force reports are the same rows it found.");
            Assert.That(
                await context.PlayerReports.CountAsync(r => r.GameId == _gameId && r.ReportType == PlayerReportType.CombatSighted),
                Is.EqualTo(1),
                "And the watcher's combat report was raised alongside them.");
        }
    }

    [Test, Description("A Turn in which no engagement started, changed or ended succeeds and writes nothing, since there is no history for anyone to have watched.")]
    public async Task ShouldSucceedAndWriteNothingForATurnWithNoBattleHistoryAsync()
    {
        await FoldCombatAsync();

        await using AscendantEmpireContext context = await DbFactory.CreateDbContextAsync();

        Assert.That(await context.PlayerReports.AnyAsync(r => r.GameId == _gameId), Is.False);
    }

    private int TileId(int x, int y)
    {
        return _campaignMapOffsetId + x + (y * _MAP_WIDTH);
    }

    private async Task FoldCombatAsync()
    {
        ServiceResultBase result = await _combatFoldService.FoldTurnAsync(_gameId, _playerTurnId, _actingPlayerId);

        Assert.That(result.HasSucceeded, Is.True, () => string.Join("; ", result.Errors));
    }

    private async Task FoldForcesAsync()
    {
        ServiceResultBase result = await _forceFoldService.FoldTurnAsync(_gameId, _playerTurnId, _actingPlayerId);

        Assert.That(result.HasSucceeded, Is.True, () => string.Join("; ", result.Errors));
    }

    /// <summary>Writes a Battle between the acting Player and the opponent to the table and returns its id, concluded this Round when asked.</summary>
    private async Task<int> SeedBattleAsync(int x, bool concluded)
    {
        await using AscendantEmpireContext context = await DbFactory.CreateDbContextAsync();

        DateTimeOffset now = DateTimeOffset.UtcNow;

        var battle = new Battle
        {
            GameId = _gameId,
            CampaignMapTileId = TileId(x, _BATTLE_ROW),
            CommencedAtRoundNumber = 1,
            ConcludedAtRoundNumber = concluded ? 1 : null,
            VictorId = concluded ? _actingPlayerId : null,
            CreatedAt = now,
            UpdatedAt = now,
            RowVersion = [],
            BattleParticipants =
            [
                .. new[] { _actingPlayerId, _opposingPlayerId }.Select(playerId => new BattleParticipant
                {
                    PlayerId = playerId,
                    JoinedAtRoundNumber = 1,
                    IsStillInBattle = concluded == false,
                    LeftAtRoundNumber = concluded ? 1 : null,
                    ExitReason = concluded ? BattleExitReason.CeaseFire : BattleExitReason.NotApplicable,
                    RowVersion = []
                })
            ]
        };

        context.Battles.Add(battle);
        await context.SaveChangesAsync();

        return battle.Id;
    }

    /// <summary>Writes history rows for one engagement against the open Turn, on the tile it is being fought over.</summary>
    private async Task SeedHistoryAsync(int battleId, int x, params (BattleHistoryEventType EventType, int? Participant, int? TroopsLost, int? GroundHeldBy, int Sequence)[] events)
    {
        await using AscendantEmpireContext context = await DbFactory.CreateDbContextAsync();

        context.BattleHistory.AddRange(events.Select(historyEvent => new BattleHistory
        {
            GameId = _gameId,
            BattleId = battleId,
            EventType = historyEvent.EventType,
            ParticipantPlayerId = historyEvent.Participant,
            ExitReason = historyEvent.EventType == BattleHistoryEventType.ParticipantLeft ? BattleExitReason.Routed : null,
            TroopsLost = historyEvent.TroopsLost,
            GroundHeldByPlayerId = historyEvent.GroundHeldBy,
            CampaignMapTileId = TileId(x, _BATTLE_ROW),
            PlayerTurnId = _playerTurnId,
            TurnSequence = historyEvent.Sequence
        }));

        await context.SaveChangesAsync();
    }

    /// <summary>Writes Moved rows for one of the acting Player's settler-template Forces against the open Turn.</summary>
    private async Task SeedMovementAsync(int unitInstanceId, params ((int X, int Y) From, (int X, int Y) To, int Sequence)[] moves)
    {
        await using AscendantEmpireContext context = await DbFactory.CreateDbContextAsync();

        context.MovementHistory.AddRange(moves.Select(move => new MovementHistory
        {
            GameId = _gameId,
            UnitInstanceId = unitInstanceId,
            PlayerId = _actingPlayerId,
            UnitTemplateId = _settlerTemplateId,
            EventType = ForceHistoryEventType.Moved,
            FromCampaignMapTileId = TileId(move.From.X, move.From.Y),
            CampaignMapTileId = TileId(move.To.X, move.To.Y),
            PlayerTurnId = _playerTurnId,
            TurnSequence = move.Sequence
        }));

        await context.SaveChangesAsync();
    }
}
