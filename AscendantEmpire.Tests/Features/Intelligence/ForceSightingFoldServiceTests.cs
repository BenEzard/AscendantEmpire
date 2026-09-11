using AscendantEmpire.Application.Common;
using AscendantEmpire.Application.Data;
using AscendantEmpire.Application.Features.Game.Handlers;
using AscendantEmpire.Application.Features.Intelligence.Services;
using AscendantEmpire.Application.Features.Maps.CampaignMaps.Services;
using AscendantEmpire.Domain.Entities.Games;
using AscendantEmpire.Domain.Entities.Units;
using AscendantEmpire.Domain.Enums.Forces;
using AscendantEmpire.Domain.Enums.Intelligence;
using AscendantEmpire.Domain.Enums.Maps;
using AscendantEmpire.Tests.Features.Maps.CampaignMaps;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;

using static AscendantEmpire.Application.Common.ServiceResultBase;

namespace AscendantEmpire.Tests.Features.Intelligence;

/// <summary>
/// End-to-end tests of <see cref="ForceSightingFoldService"/> over a real Game: history in, reports and known Forces out.
/// </summary>
/// <remarks>
/// <para>
/// On the containerised tier, for two of the reasons <c>testing.md</c> names: the step replaces a Turn's reports with <c>ExecuteDeleteAsync</c>, inside an explicit transaction. The witness service
/// and the sight rules underneath it are the real ones, so a report here means the Force was genuinely in view.
/// </para>
/// <para>
/// The map is 10 x 3 of Flat Grassland. The watching Player's starting Scout (Sight 3) and Settler (Sight 1) are moved to (8, 1), so between them they see columns 5 to 9 and nothing west of
/// that. History rows are written straight to the table for the acting Player, the same way <c>GameDeleteExecutorTests</c> seeds them: this fixture is about what the step makes of a history, not
/// how the history was written.
/// </para>
/// </remarks>
[TestFixture]
internal class ForceSightingFoldServiceTests : GameSqlServerTestBase
{
    private const int _MAP_WIDTH = 10;
    private const int _MAP_HEIGHT = 3;

    /// <summary>A Force id no row has: one destroyed and hard-deleted before the step ran.</summary>
    private const int _DESTROYED_UNIT_INSTANCE_ID = 999_999;

    private ForceSightingFoldService _foldService = null!;

    private int _gameId;
    private int _campaignMapOffsetId;
    private int _actingPlayerId;
    private int _watchingPlayerId;
    private int _settlerId;
    private int _settlerHealthyCount;
    private int _settlerTemplateId;
    private int _playerTurnId;

    [SetUp]
    public async Task SetUpGameAsync()
    {
        await SeedGameAsync(mapWidth: _MAP_WIDTH, mapHeight: _MAP_HEIGHT, numberOfPlayers: 2);

        ServiceResult<int> setupResult = await _service.SetupGameAsync(
            new SetupGameRequest(GalleryMapId: _galleryIndexId, GameName: "Watched Game", NumberOfPlayers: 2, BattleMapSize: 50, GenerationSeed: null),
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

            var settler = await context.UnitInstances
                .Where(u => u.GameId == _gameId && u.PlayerId == _actingPlayerId && u.UnitTemplateId == _settlerTemplateId)
                .Select(u => new { u.Id, u.HealthyCount })
                .FirstAsync();

            _settlerId = settler.Id;
            _settlerHealthyCount = settler.HealthyCount;

            // Every one of the watcher's starting Forces onto one tile, so what they can see is fixed and easy to reason about.
            await context.UnitInstances
                .Where(u => u.GameId == _gameId && u.PlayerId == _watchingPlayerId)
                .ExecuteUpdateAsync(setters => setters.SetProperty(u => u.CampaignMapTileId, TileId(8, 1)));
        }

        // The acting Player is the lowest id, so this opens their Turn.
        _playerTurnId = await OpenFirstPlayerTurnAsync(_gameId);

        var sightDataReader = new SightDataReader();
        var fieldOfViewService = new FieldOfViewService(NullLogger<FieldOfViewService>.Instance, DbFactory, sightDataReader, SightTestData.AllStrategies);
        var witnessService = new WitnessService(NullLogger<WitnessService>.Instance, DbFactory, sightDataReader, new SoloCoalitionResolver(), fieldOfViewService);

        _foldService = new ForceSightingFoldService(NullLogger<ForceSightingFoldService>.Instance, DbFactory, sightDataReader, witnessService);
    }

    [Test, Description("A Force that walks into the watcher's view and back out raises one report for the watcher, with its path and heading, and none for the Player who moved it.")]
    public async Task ShouldRaiseAReportForTheWatchingPlayerAndNoneForTheActingPlayerAsync()
    {
        await SeedWanderIntoViewAndOutAsync();

        await FoldAsync();

        await using AscendantEmpireContext context = await DbFactory.CreateDbContextAsync();

        PlayerReport report = await context.PlayerReports.Include(r => r.PathTiles).SingleAsync(r => r.GameId == _gameId);
        PlayerKnownForce knownForce = await context.PlayerKnownForces.SingleAsync(k => k.GameId == _gameId);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(report.RecipientPlayerId, Is.EqualTo(_watchingPlayerId), "The acting Player gets no report of what they did themselves.");
            Assert.That(report.PathTiles.OrderBy(t => t.Ordinal).Select(t => t.CampaignMapTileId), Is.EqualTo(new[] { TileId(5, 1), TileId(6, 1), TileId(5, 1) }));
            Assert.That(report.ExitDirection, Is.EqualTo(MoveDirection.West), "It was last seen stepping from column 5 to column 4.");
            Assert.That(report.StillVisible, Is.False);
            Assert.That(report.SubjectUnitInstanceId, Is.EqualTo(_settlerId));
            Assert.That(report.SubjectOwnerPlayerId, Is.EqualTo(_actingPlayerId));
            Assert.That(report.SubjectHealthyTroops, Is.EqualTo(_settlerHealthyCount));
            Assert.That(report.FirstWitnessOwnerPlayerId, Is.EqualTo(_watchingPlayerId));

            Assert.That(knownForce.PlayerId, Is.EqualTo(_watchingPlayerId));
            Assert.That(knownForce.LastSeenCampaignMapTileId, Is.EqualTo(TileId(5, 1)));
            Assert.That(knownForce.LastSeenTurnSequence, Is.EqualTo(4));
            Assert.That(knownForce.Status, Is.EqualTo(KnownForceStatus.Sighted));
        }
    }

    [Test, Description("Ending a Turn is not one transaction and can be retried, so folding the same Turn twice leaves one set of reports rather than two.")]
    public async Task ShouldReplaceTheTurnsReportsRatherThanAddToThemWhenFoldedAgainAsync()
    {
        await SeedWanderIntoViewAndOutAsync();

        await FoldAsync();
        await FoldAsync();

        await using AscendantEmpireContext context = await DbFactory.CreateDbContextAsync();

        using (Assert.EnterMultipleScope())
        {
            Assert.That(await context.PlayerReports.CountAsync(r => r.GameId == _gameId), Is.EqualTo(1));
            Assert.That(await context.PlayerReportPathTiles.CountAsync(), Is.EqualTo(3));
            Assert.That(await context.PlayerKnownForces.CountAsync(k => k.GameId == _gameId), Is.EqualTo(1));
        }
    }

    [Test, Description("A Force watched and then destroyed before the step ran is still reported, and remembered as destroyed, because its history outlives it.")]
    public async Task ShouldStillReportAForceThatWasDestroyedBeforeTheFoldRanAsync()
    {
        await SeedHistoryAsync(
            _DESTROYED_UNIT_INSTANCE_ID,
            (ForceHistoryEventType.Moved, From: (6, 0), To: (6, 1), Sequence: 1),
            (ForceHistoryEventType.Destroyed, From: null, To: (6, 1), Sequence: 2));

        await FoldAsync();

        await using AscendantEmpireContext context = await DbFactory.CreateDbContextAsync();

        PlayerReport report = await context.PlayerReports.Include(r => r.PathTiles).SingleAsync(r => r.GameId == _gameId);
        PlayerKnownForce knownForce = await context.PlayerKnownForces.SingleAsync(k => k.GameId == _gameId);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(report.SubjectUnitInstanceId, Is.EqualTo(_DESTROYED_UNIT_INSTANCE_ID));
            Assert.That(report.PathTiles.OrderBy(t => t.Ordinal).Select(t => t.CampaignMapTileId), Is.EqualTo(new[] { TileId(6, 0), TileId(6, 1) }));
            Assert.That(report.DestroyedInView, Is.True);
            Assert.That(report.SubjectHealthyTroops, Is.Null, "There was no Force left to read a strength from.");
            Assert.That(knownForce.Status, Is.EqualTo(KnownForceStatus.Destroyed));
        }
    }

    [Test, Description("An older sighting never overwrites a newer memory of the same Force, so a Player's knowledge is never rolled backwards.")]
    public async Task ShouldNotOverwriteANewerKnownForceWithAnOlderSightingAsync()
    {
        await SeedWanderIntoViewAndOutAsync();

        await using (AscendantEmpireContext seedContext = await DbFactory.CreateDbContextAsync())
        {
            seedContext.PlayerKnownForces.Add(new PlayerKnownForce
            {
                GameId = _gameId,
                PlayerId = _watchingPlayerId,
                UnitInstanceId = _settlerId,
                OwnerPlayerId = _actingPlayerId,
                UnitTemplateId = _settlerTemplateId,
                Status = KnownForceStatus.Sighted,
                Fidelity = SightingFidelity.Detail,
                LastSeenCampaignMapTileId = TileId(9, 2),
                LastSeenPlayerTurnId = _playerTurnId,
                // Later in the same Turn than anything the seeded history reaches.
                LastSeenTurnSequence = 99
            });

            await seedContext.SaveChangesAsync();
        }

        await FoldAsync();

        await using AscendantEmpireContext context = await DbFactory.CreateDbContextAsync();

        PlayerKnownForce knownForce = await context.PlayerKnownForces.SingleAsync(k => k.GameId == _gameId);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(knownForce.LastSeenCampaignMapTileId, Is.EqualTo(TileId(9, 2)));
            Assert.That(knownForce.LastSeenTurnSequence, Is.EqualTo(99));
            Assert.That(await context.PlayerReports.CountAsync(r => r.GameId == _gameId), Is.EqualTo(1), "The report is still raised; only the memory keeps its newer value.");
        }
    }

    [Test, Description("A Force that never came within anybody's sight raises nothing at all, so an unwatched Turn stays out of every feed.")]
    public async Task ShouldRaiseNothingWhenNobodyCouldSeeTheForceAsync()
    {
        await SeedHistoryAsync(
            _settlerId,
            (ForceHistoryEventType.Moved, From: (0, 2), To: (1, 2), Sequence: 1),
            (ForceHistoryEventType.Moved, From: (1, 2), To: (2, 2), Sequence: 2));

        await FoldAsync();

        await using AscendantEmpireContext context = await DbFactory.CreateDbContextAsync();

        using (Assert.EnterMultipleScope())
        {
            Assert.That(await context.PlayerReports.AnyAsync(r => r.GameId == _gameId), Is.False);
            Assert.That(await context.PlayerKnownForces.AnyAsync(k => k.GameId == _gameId), Is.False);
        }
    }

    // ---------------------------------------------------------------------

    private int TileId(int x, int y)
    {
        return _campaignMapOffsetId + x + (y * _MAP_WIDTH);
    }

    private async Task FoldAsync()
    {
        ServiceResultBase result = await _foldService.FoldTurnAsync(_gameId, _playerTurnId, _actingPlayerId);

        Assert.That(result.HasSucceeded, Is.True, () => string.Join("; ", result.Errors));
    }

    /// <summary>
    /// The acting Player's Settler walks east from (3, 1) into view at column 5, on to column 6, back to 5, and out of view west to column 4.
    /// </summary>
    private Task SeedWanderIntoViewAndOutAsync()
    {
        return SeedHistoryAsync(
            _settlerId,
            (ForceHistoryEventType.Moved, From: (3, 1), To: (4, 1), Sequence: 1),
            (ForceHistoryEventType.Moved, From: (4, 1), To: (5, 1), Sequence: 2),
            (ForceHistoryEventType.Moved, From: (5, 1), To: (6, 1), Sequence: 3),
            (ForceHistoryEventType.Moved, From: (6, 1), To: (5, 1), Sequence: 4),
            (ForceHistoryEventType.Moved, From: (5, 1), To: (4, 1), Sequence: 5));
    }

    /// <summary>Writes history rows for one of the acting Player's settler-template Forces against the open Turn.</summary>
    private async Task SeedHistoryAsync(int unitInstanceId, params (ForceHistoryEventType EventType, (int X, int Y)? From, (int X, int Y) To, int Sequence)[] events)
    {
        await using AscendantEmpireContext context = await DbFactory.CreateDbContextAsync();

        context.MovementHistory.AddRange(events.Select(historyEvent => new MovementHistory
        {
            GameId = _gameId,
            UnitInstanceId = unitInstanceId,
            PlayerId = _actingPlayerId,
            UnitTemplateId = _settlerTemplateId,
            EventType = historyEvent.EventType,
            FromCampaignMapTileId = historyEvent.From is (int X, int Y) from ? TileId(from.X, from.Y) : null,
            CampaignMapTileId = TileId(historyEvent.To.X, historyEvent.To.Y),
            PlayerTurnId = _playerTurnId,
            TurnSequence = historyEvent.Sequence
        }));

        await context.SaveChangesAsync();
    }
}
