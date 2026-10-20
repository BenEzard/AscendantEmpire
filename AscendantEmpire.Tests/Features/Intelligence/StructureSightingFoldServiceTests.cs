using AscendantEmpire.Application.Common;
using AscendantEmpire.Application.Data;
using AscendantEmpire.Application.Features.Game.Handlers;
using AscendantEmpire.Application.Features.Intelligence.Services;
using AscendantEmpire.Application.Features.Maps.CampaignMaps.Services;
using AscendantEmpire.Domain.Entities.Games;
using AscendantEmpire.Domain.Entities.Structures;
using AscendantEmpire.Domain.Entities.Units;
using AscendantEmpire.Domain.Enums;
using AscendantEmpire.Domain.Enums.Forces;
using AscendantEmpire.Domain.Enums.Intelligence;
using AscendantEmpire.Domain.Enums.Maps;
using AscendantEmpire.Domain.Enums.Structures;
using AscendantEmpire.Tests.Features.Maps.CampaignMaps;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;

using static AscendantEmpire.Application.Common.ServiceResultBase;

namespace AscendantEmpire.Tests.Features.Intelligence;

/// <summary>
/// End-to-end tests of <see cref="StructureSightingFoldService"/> over a real Game: structure history in, structure reports and known Structures out.
/// </summary>
/// <remarks>
/// <para>
/// On the containerised tier, for two of the reasons <c>testing.md</c> names: the step replaces a Turn's reports with <c>ExecuteDeleteAsync</c>, inside an explicit transaction. The witness service
/// and the sight rules underneath it are the real ones, so a report here means the Structure was genuinely in view.
/// </para>
/// <para>
/// The map is 10 x 3 of Flat Grassland, set up exactly as in <see cref="ForceSightingFoldServiceTests"/>: the watching Player's starting Forces stand at (8, 1), so between them they see columns 5 to
/// 9 and nothing west of that. Structures and their history are written straight to the tables: this fixture is about what the step makes of a history, not how the history was written.
/// </para>
/// </remarks>
[TestFixture]
internal class StructureSightingFoldServiceTests : GameSqlServerTestBase
{
    private const int _MAP_WIDTH = 10;
    private const int _MAP_HEIGHT = 3;

    private StructureSightingFoldService _structureFoldService = null!;
    private ForceSightingFoldService _forceFoldService = null!;

    private int _gameId;
    private int _campaignMapOffsetId;
    private int _actingPlayerId;
    private int _watchingPlayerId;
    private int _settlerId;
    private int _settlerTemplateId;
    private int _watchtowerStructureId;
    private int _playerTurnId;

    [SetUp]
    public async Task SetUpGameAsync()
    {
        await SeedGameAsync(mapWidth: _MAP_WIDTH, mapHeight: _MAP_HEIGHT, numberOfPlayers: 2);

        ServiceResult<int> setupResult = await _service.SetupGameAsync(
            new SetupGameRequest(GalleryMapId: _galleryIndexId, GameName: "Watched Structures Game", NumberOfPlayers: 2, BattleMapSize: 50, GenerationSeed: null),
            CancellationToken.None);

        Assert.That(setupResult.HasSucceeded, Is.True, () => $"{string.Join("; ", setupResult.Errors)} Logged exception: {_capturingLogger.LastException}");

        _gameId = setupResult.SuccessValue;

        await using (AscendantEmpireContext context = await DbFactory.CreateDbContextAsync())
        {
            _campaignMapOffsetId = await context.GameIndex.Where(g => g.Id == _gameId).Select(g => g.CampaignMapOffsetId).SingleAsync();

            int[] playerIds = await context.Players.Where(p => p.GameId == _gameId).OrderBy(p => p.Id).Select(p => p.Id).ToArrayAsync();
            _actingPlayerId = playerIds[0];
            _watchingPlayerId = playerIds[1];

            _settlerTemplateId = await context.UnitTemplates.Where(t => t.Code == "SETTLER").Select(t => t.Id).SingleAsync();
            _settlerId = await context.UnitInstances
                .Where(u => u.GameId == _gameId && u.PlayerId == _actingPlayerId && u.UnitTemplateId == _settlerTemplateId)
                .Select(u => u.Id)
                .FirstAsync();

            // Every one of the watcher's starting Forces onto one tile, so what they can see is fixed and easy to reason about.
            await context.UnitInstances
                .Where(u => u.GameId == _gameId && u.PlayerId == _watchingPlayerId)
                .ExecuteUpdateAsync(setters => setters.SetProperty(u => u.CampaignMapTileId, TileId(8, 1)));

            // The structure catalogue is authored data the app never creates, so the test stands one in: a watchtower that sees two tiles.
            var watchtower = new Structure
            {
                Code = "watchtower",
                Name = "Watchtower",
                RequiresSlotType = TileSlotType.Primary,
                AllowedPositions = PositionOnTile.WholeTile,
                CanBeBuiltOnCampaignMap = true,
                CanBeBuiltOnBattleMap = false,
                DualMapBuildTrigger = false,
                BuildPointsRequired = 0,
                SightRange = 2,
                SightDirection = DetectionDirection.FullRadius,
                Asserts = StructureAssertion.Owned,
                MapImagePath = "watchtower.png",
                FullImagePath = "watchtower-full.png"
            };

            context.Set<Structure>().Add(watchtower);
            await context.SaveChangesAsync();

            _watchtowerStructureId = watchtower.Id;
        }

        // The acting Player is the lowest id, so this opens their Turn.
        _playerTurnId = await OpenFirstPlayerTurnAsync(_gameId);

        var sightDataReader = new SightDataReader();
        var fieldOfViewService = new FieldOfViewService(NullLogger<FieldOfViewService>.Instance, DbFactory, sightDataReader, SightTestData.AllStrategies);
        var witnessService = new WitnessService(NullLogger<WitnessService>.Instance, DbFactory, sightDataReader, new SoloCoalitionResolver(), fieldOfViewService);

        _structureFoldService = new StructureSightingFoldService(NullLogger<StructureSightingFoldService>.Instance, DbFactory, sightDataReader, witnessService);
        _forceFoldService = new ForceSightingFoldService(NullLogger<ForceSightingFoldService>.Instance, DbFactory, sightDataReader, witnessService);
    }

    [Test, Description("A village founded and razed in the watcher's view within one Turn is one report saying both, and remembered as razed - the transient case that used to leave no trace.")]
    public async Task ShouldRaiseOneReportForAStructureFoundedAndRazedInViewAsync()
    {
        int structureInstanceId = await SeedStructureAsync(x: 6, y: 1, _actingPlayerId, removed: true);

        await SeedHistoryAsync(
            structureInstanceId,
            (StructureHistoryEventType.Founded, _actingPlayerId, PreviousOwner: null, Completion: 30, Sequence: 3),
            (StructureHistoryEventType.Razed, _actingPlayerId, PreviousOwner: null, Completion: 30, Sequence: 9));

        await FoldStructuresAsync();

        await using AscendantEmpireContext context = await DbFactory.CreateDbContextAsync();

        PlayerReport report = await context.PlayerReports.SingleAsync(r => r.GameId == _gameId);
        PlayerKnownStructure known = await context.PlayerKnownStructures.SingleAsync(k => k.GameId == _gameId);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(report.RecipientPlayerId, Is.EqualTo(_watchingPlayerId), "The acting Player gets no report of what they did themselves.");
            Assert.That(report.ReportType, Is.EqualTo(PlayerReportType.StructureSighted));
            Assert.That(report.SubjectStructureInstanceId, Is.EqualTo(structureInstanceId));
            Assert.That(report.SubjectStructureId, Is.EqualTo(_watchtowerStructureId));
            Assert.That(report.SubjectOwnerPlayerId, Is.EqualTo(_actingPlayerId));
            Assert.That(report.AppearedInView, Is.True);
            Assert.That(report.DestroyedInView, Is.True);
            Assert.That(report.CompletedInView, Is.False);
            Assert.That(report.CapturedInView, Is.False);
            Assert.That(report.StillVisible, Is.False);
            Assert.That(report.FirstCampaignMapTileId, Is.EqualTo(TileId(6, 1)));
            Assert.That((report.FirstTurnSequence, report.LastTurnSequence), Is.EqualTo((3, 9)));

            Assert.That(known.PlayerId, Is.EqualTo(_watchingPlayerId));
            Assert.That(known.Status, Is.EqualTo(KnownStructureStatus.Razed));
            Assert.That(known.LastSeenTurnSequence, Is.EqualTo(9));
        }
    }

    [Test, Description("A village founded in view and still standing at the end is reported and remembered as standing and still in sight - the persistent case a Player would otherwise miss.")]
    public async Task ShouldReportAndRememberAStructureFoundedInViewAndStillStandingAsync()
    {
        int structureInstanceId = await SeedStructureAsync(x: 6, y: 1, _actingPlayerId);

        await SeedHistoryAsync(structureInstanceId, (StructureHistoryEventType.Founded, _actingPlayerId, PreviousOwner: null, Completion: 30, Sequence: 2));

        await FoldStructuresAsync();

        await using AscendantEmpireContext context = await DbFactory.CreateDbContextAsync();

        PlayerReport report = await context.PlayerReports.SingleAsync(r => r.GameId == _gameId);
        PlayerKnownStructure known = await context.PlayerKnownStructures.SingleAsync(k => k.GameId == _gameId);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(report.AppearedInView, Is.True);
            Assert.That(report.StillVisible, Is.True);
            Assert.That(report.SubjectCompletionPercent, Is.EqualTo(30));

            Assert.That(known.Status, Is.EqualTo(KnownStructureStatus.Standing));
            Assert.That(known.StillVisible, Is.True);
            Assert.That(known.ObservedCompletionPercent, Is.EqualTo(30));
            Assert.That(known.OwnerPlayerId, Is.EqualTo(_actingPlayerId));
        }
    }

    [Test, Description("A Structure founded and finished in one Turn is a single report flagged both ways, so the feed can say it was completed without also saying construction started.")]
    public async Task ShouldRaiseOneReportForAStructureFoundedAndFinishedInOneTurnAsync()
    {
        int structureInstanceId = await SeedStructureAsync(x: 6, y: 1, _actingPlayerId);

        await SeedHistoryAsync(
            structureInstanceId,
            (StructureHistoryEventType.Founded, _actingPlayerId, PreviousOwner: null, Completion: 50, Sequence: 1),
            (StructureHistoryEventType.Progressed, _actingPlayerId, PreviousOwner: null, Completion: 80, Sequence: 2),
            (StructureHistoryEventType.Completed, _actingPlayerId, PreviousOwner: null, Completion: 100, Sequence: 3));

        await FoldStructuresAsync();

        await using AscendantEmpireContext context = await DbFactory.CreateDbContextAsync();

        PlayerReport report = await context.PlayerReports.SingleAsync(r => r.GameId == _gameId);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(report.AppearedInView, Is.True);
            Assert.That(report.CompletedInView, Is.True);
            Assert.That(report.SubjectCompletionPercent, Is.EqualTo(100));
        }
    }

    [Test, Description("Construction watched advancing without starting or finishing raises no report, but the Player's memory of how far built it is still moves on.")]
    public async Task ShouldOnlyRefreshTheMemoryWhenOnlyProgressWasSeenAsync()
    {
        int structureInstanceId = await SeedStructureAsync(x: 6, y: 1, _actingPlayerId);

        await SeedHistoryAsync(structureInstanceId, (StructureHistoryEventType.Progressed, _actingPlayerId, PreviousOwner: null, Completion: 60, Sequence: 4));

        await FoldStructuresAsync();

        await using AscendantEmpireContext context = await DbFactory.CreateDbContextAsync();

        using (Assert.EnterMultipleScope())
        {
            Assert.That(await context.PlayerReports.AnyAsync(r => r.GameId == _gameId), Is.False);
            Assert.That((await context.PlayerKnownStructures.SingleAsync(k => k.GameId == _gameId)).ObservedCompletionPercent, Is.EqualTo(60));
        }
    }

    [Test, Description("A Player watching their own Structure captured is told who took it from them, because an event done to another Player's Structure is not hidden from that Player.")]
    public async Task ShouldReportACaptureToThePlayerWhoWatchedTheirStructureTakenAsync()
    {
        int structureInstanceId = await SeedStructureAsync(x: 6, y: 1, _actingPlayerId);

        await SeedHistoryAsync(structureInstanceId, (StructureHistoryEventType.Captured, _actingPlayerId, PreviousOwner: _watchingPlayerId, Completion: 100, Sequence: 4));

        await FoldStructuresAsync();

        await using AscendantEmpireContext context = await DbFactory.CreateDbContextAsync();

        PlayerReport report = await context.PlayerReports.SingleAsync(r => r.GameId == _gameId);
        PlayerKnownStructure known = await context.PlayerKnownStructures.SingleAsync(k => k.GameId == _gameId);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(report.RecipientPlayerId, Is.EqualTo(_watchingPlayerId));
            Assert.That(report.CapturedInView, Is.True);
            Assert.That(report.SubjectOwnerPlayerId, Is.EqualTo(_actingPlayerId));
            Assert.That(report.SubjectPreviousOwnerPlayerId, Is.EqualTo(_watchingPlayerId));
            Assert.That(known.OwnerPlayerId, Is.EqualTo(_actingPlayerId), "They remember it as the captor's now.");
        }
    }

    [Test, Description("Folding a Turn's Structures again replaces their reports, and leaves the same Turn's Force reports alone, since each step owns only its own report type.")]
    public async Task ShouldReplaceTheTurnsStructureReportsWithoutTouchingItsForceReportsAsync()
    {
        int structureInstanceId = await SeedStructureAsync(x: 6, y: 1, _actingPlayerId);

        await SeedHistoryAsync(structureInstanceId, (StructureHistoryEventType.Founded, _actingPlayerId, PreviousOwner: null, Completion: 30, Sequence: 1));
        await SeedMovementAsync(_settlerId, ((5, 0), (6, 0), Sequence: 2));

        await FoldForcesAsync();
        await FoldStructuresAsync();
        await FoldStructuresAsync();
        await FoldForcesAsync();

        await using AscendantEmpireContext context = await DbFactory.CreateDbContextAsync();

        using (Assert.EnterMultipleScope())
        {
            Assert.That(await context.PlayerReports.CountAsync(r => r.GameId == _gameId && r.ReportType == PlayerReportType.StructureSighted), Is.EqualTo(1));
            Assert.That(await context.PlayerReports.CountAsync(r => r.GameId == _gameId && r.ReportType == PlayerReportType.ForceSighted), Is.EqualTo(1));
            Assert.That(await context.PlayerKnownStructures.CountAsync(k => k.GameId == _gameId), Is.EqualTo(1));
        }
    }

    [Test, Description("An older sighting never overwrites a newer memory of the same Structure, so a Player's knowledge is never rolled backwards.")]
    public async Task ShouldNotOverwriteANewerKnownStructureWithAnOlderSightingAsync()
    {
        int structureInstanceId = await SeedStructureAsync(x: 6, y: 1, _actingPlayerId);

        await SeedHistoryAsync(structureInstanceId, (StructureHistoryEventType.Founded, _actingPlayerId, PreviousOwner: null, Completion: 30, Sequence: 1));

        await using (AscendantEmpireContext seedContext = await DbFactory.CreateDbContextAsync())
        {
            seedContext.PlayerKnownStructures.Add(new PlayerKnownStructure
            {
                GameId = _gameId,
                PlayerId = _watchingPlayerId,
                StructureInstanceId = structureInstanceId,
                StructureId = _watchtowerStructureId,
                OwnerPlayerId = _actingPlayerId,
                ObservedCompletionPercent = 90,
                Status = KnownStructureStatus.Standing,
                Fidelity = SightingFidelity.Detail,
                CampaignMapTileId = TileId(6, 1),
                LastSeenPlayerTurnId = _playerTurnId,
                // Later in the same Turn than anything the seeded history reaches.
                LastSeenTurnSequence = 99
            });

            await seedContext.SaveChangesAsync();
        }

        await FoldStructuresAsync();

        await using AscendantEmpireContext context = await DbFactory.CreateDbContextAsync();

        PlayerKnownStructure known = await context.PlayerKnownStructures.SingleAsync(k => k.GameId == _gameId);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(known.ObservedCompletionPercent, Is.EqualTo(90));
            Assert.That(known.LastSeenTurnSequence, Is.EqualTo(99));
            Assert.That(await context.PlayerReports.CountAsync(r => r.GameId == _gameId), Is.EqualTo(1), "The report is still raised; only the memory keeps its newer value.");
        }
    }

    [Test, Description("A Structure founded where nobody could see it raises nothing at all, so an unwatched change stays out of every feed.")]
    public async Task ShouldRaiseNothingForAStructureNobodyCouldSeeAsync()
    {
        int structureInstanceId = await SeedStructureAsync(x: 1, y: 1, _actingPlayerId);

        await SeedHistoryAsync(structureInstanceId, (StructureHistoryEventType.Founded, _actingPlayerId, PreviousOwner: null, Completion: 30, Sequence: 1));

        await FoldStructuresAsync();

        await using AscendantEmpireContext context = await DbFactory.CreateDbContextAsync();

        using (Assert.EnterMultipleScope())
        {
            Assert.That(await context.PlayerReports.AnyAsync(r => r.GameId == _gameId), Is.False);
            Assert.That(await context.PlayerKnownStructures.AnyAsync(k => k.GameId == _gameId), Is.False);
        }
    }

    [Test, Description("A watchtower razed later in the Turn still reports the Force it watched before it fell, so the acting Player cannot erase what was seen by razing the witness.")]
    public async Task ShouldStillReportAForceWatchedByATowerRazedLaterInTheTurnAsync()
    {
        // The watcher's own tower in the west, where none of their Forces can see: it alone watches columns 0 to 4.
        int towerId = await SeedStructureAsync(x: 2, y: 1, _watchingPlayerId, removed: true);

        await SeedMovementAsync(_settlerId, ((0, 1), (1, 1), Sequence: 3));
        await SeedHistoryAsync(towerId, (StructureHistoryEventType.Razed, _watchingPlayerId, PreviousOwner: null, Completion: 100, Sequence: 9));

        await FoldForcesAsync();

        await using AscendantEmpireContext context = await DbFactory.CreateDbContextAsync();

        PlayerReport report = await context.PlayerReports.SingleAsync(r => r.GameId == _gameId && r.ReportType == PlayerReportType.ForceSighted);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(report.RecipientPlayerId, Is.EqualTo(_watchingPlayerId));
            Assert.That(report.FirstWitnessRecordType, Is.EqualTo(RecordType.Structure));
            Assert.That(report.FirstWitnessId, Is.EqualTo(towerId), "The tower saw the move at 3, before it was razed at 9.");
        }
    }

    // ---------------------------------------------------------------------

    private int TileId(int x, int y)
    {
        return _campaignMapOffsetId + x + (y * _MAP_WIDTH);
    }

    private async Task FoldStructuresAsync()
    {
        ServiceResultBase result = await _structureFoldService.FoldTurnAsync(_gameId, _playerTurnId, _actingPlayerId);

        Assert.That(result.HasSucceeded, Is.True, () => string.Join("; ", result.Errors));
    }

    private async Task FoldForcesAsync()
    {
        ServiceResultBase result = await _forceFoldService.FoldTurnAsync(_gameId, _playerTurnId, _actingPlayerId);

        Assert.That(result.HasSucceeded, Is.True, () => string.Join("; ", result.Errors));
    }

    /// <summary>Writes a watchtower to the table and returns its id, razed during the open Turn when asked.</summary>
    private async Task<int> SeedStructureAsync(int x, int y, int ownerPlayerId, bool removed = false)
    {
        await using AscendantEmpireContext context = await DbFactory.CreateDbContextAsync();

        var structure = new StructureInstance
        {
            GameId = _gameId,
            CampaignMapTileId = TileId(x, y),
            StructureId = _watchtowerStructureId,
            SlotType = TileSlotType.Primary,
            Position = PositionOnTile.WholeTile,
            OwnedByPlayerId = ownerPlayerId,
            CreatedOnTurnId = _playerTurnId,
            RemovedOnTurnId = removed ? _playerTurnId : null
        };

        context.StructureInstances.Add(structure);
        await context.SaveChangesAsync();

        return structure.Id;
    }

    /// <summary>Writes history rows for one Structure against the open Turn, on the Structure's own tile.</summary>
    private async Task SeedHistoryAsync(int structureInstanceId, params (StructureHistoryEventType EventType, int Owner, int? PreviousOwner, double Completion, int Sequence)[] events)
    {
        await using AscendantEmpireContext context = await DbFactory.CreateDbContextAsync();

        int tileId = await context.StructureInstances.Where(s => s.Id == structureInstanceId).Select(s => s.CampaignMapTileId).SingleAsync();

        context.StructureHistory.AddRange(events.Select(historyEvent => new StructureHistory
        {
            GameId = _gameId,
            StructureInstanceId = structureInstanceId,
            StructureId = _watchtowerStructureId,
            PlayerId = historyEvent.Owner,
            PreviousOwnerPlayerId = historyEvent.PreviousOwner,
            EventType = historyEvent.EventType,
            CompletionPercent = historyEvent.Completion,
            CampaignMapTileId = tileId,
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
