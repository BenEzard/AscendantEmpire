using AscendantEmpire.Application.Common;
using AscendantEmpire.Application.Common.Messaging;
using AscendantEmpire.Application.Data;
using AscendantEmpire.Application.Features.Game.Handlers;
using AscendantEmpire.Application.Features.Game.Services;
using AscendantEmpire.Application.Features.Players.Handlers;
using AscendantEmpire.Application.Features.Turns.Services;
using AscendantEmpire.Application.Features.Units.Handlers;
using AscendantEmpire.Application.Features.Units.Services;
using AscendantEmpire.Domain.Entities.Units;
using AscendantEmpire.Domain.Enums.Forces;
using AscendantEmpire.Domain.Enums.Turns;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

using NSubstitute;

using static AscendantEmpire.Application.Common.ServiceResultBase;

namespace AscendantEmpire.Tests.Features.Units.Services;

/// <summary>
/// Tests for what <see cref="ForceLifecycleWriter"/> and the <see cref="MovementHistory"/> table only prove against a real database.
/// </summary>
/// <remarks>
/// <para>
/// On the containerised tier, for three of the reasons <c>testing.md</c> names: a create during a Turn saves twice inside a transaction, allocating its sequence is raw SQL, and the table's
/// check constraint and deliberately missing foreign key only mean anything when a database is enforcing the rest.
/// </para>
/// <para>
/// Games are built by the real <c>GameSetupService</c>, so the Forces, tiles, templates and Turn Queue these rows point at are the ones play would give them. The behaviour that needs no database
/// is covered in <see cref="ForceLifecycleWriterTests"/>.
/// </para>
/// </remarks>
[TestFixture]
internal class ForceLifecycleWriterPersistenceTests : GameSqlServerTestBase
{
    private const int _MAP_WIDTH = 4;
    private const int _MAP_HEIGHT = 4;

    /// <summary>The seeded settler tag is unled with a base size of 25, so a spawn must be a multiple of it.</summary>
    private const int _SETTLER_SIZE = 50;

    /// <summary>A Player Turn id no row has, for provoking the foreign key on a history row.</summary>
    private const int _MISSING_PLAYER_TURN_ID = 999_999;

    private int _gameId;
    private int _playerId;
    private int _settlerTemplateId;
    private int _campaignQueueId;

    /// <summary>Captures what the spawn handler logs, because it swallows its exceptions into a generic failure that says nothing about the cause.</summary>
    private CapturingLogger<SpawnUnitHandler> _spawnLogger = null!;

    [SetUp]
    public async Task SetUpGameAsync()
    {
        _spawnLogger = new CapturingLogger<SpawnUnitHandler>();

        await SeedGameAsync(mapWidth: _MAP_WIDTH, mapHeight: _MAP_HEIGHT, numberOfPlayers: 2);

        ServiceResult<int> setupResult = await _service.SetupGameAsync(
            new SetupGameRequest(GalleryMapId: _galleryIndexId, GameName: "Lifecycle Game", NumberOfPlayers: 2, BattleMapSize: 50, GenerationSeed: null),
            CancellationToken.None);

        Assert.That(setupResult.HasSucceeded, Is.True, () => string.Join("; ", setupResult.Errors));

        _gameId = setupResult.SuccessValue;

        await using AscendantEmpireContext context = await DbFactory.CreateDbContextAsync();

        _playerId = await context.Players.Where(p => p.GameId == _gameId).OrderBy(p => p.Id).Select(p => p.Id).FirstAsync();
        _settlerTemplateId = await context.UnitTemplates.Where(t => t.Code == "SETTLER").Select(t => t.Id).SingleAsync();

        _campaignQueueId = await context.TurnQueues
            .Where(q => q.GameId == _gameId && q.QueueType == TurnQueueType.Campaign && q.BattleId == null)
            .Select(q => q.Id)
            .SingleAsync();
    }

    [Test, Description("Game setup places the starting armies with no history, because no Turn is open yet and nobody could have watched them appear.")]
    public async Task ShouldPlaceTheStartingArmiesWithoutAnyHistoryAsync()
    {
        await using AscendantEmpireContext context = await DbFactory.CreateDbContextAsync();

        using (Assert.EnterMultipleScope())
        {
            Assert.That(await context.UnitInstances.AnyAsync(u => u.GameId == _gameId), Is.True, "Setup must have placed starting Forces for this test to mean anything.");
            Assert.That(await context.MovementHistory.AnyAsync(h => h.GameId == _gameId), Is.False);
        }
    }

    [Test, Description("Forces created together during a Turn each get a Created row with their real id, sharing one sequence because they appeared at once.")]
    public async Task ShouldRecordForcesCreatedDuringATurnWithTheirRealIdsAndOneSequenceAsync()
    {
        int playerTurnId = await OpenTurnAsync();
        int tileId = await FirstTileIdAsync();

        UnitInstance[] forces = [NewSettler(tileId), NewSettler(tileId)];

        await using (AscendantEmpireContext context = await DbFactory.CreateDbContextAsync())
        {
            ServiceResultBase result = await RealWriter().CreateAsync(context, forces, playerTurnId);

            Assert.That(result.HasSucceeded, Is.True, () => string.Join("; ", result.Errors));
        }

        List<MovementHistory> rows = await LoadHistoryAsync();

        using (Assert.EnterMultipleScope())
        {
            Assert.That(rows.Select(row => row.UnitInstanceId), Is.EquivalentTo(forces.Select(force => force.Id)), "Each row carries the id its Force was given on insert.");
            Assert.That(rows, Has.All.Property(nameof(MovementHistory.EventType)).EqualTo(ForceHistoryEventType.Created));
            Assert.That(rows, Has.All.Property(nameof(MovementHistory.PlayerTurnId)).EqualTo(playerTurnId));
            Assert.That(rows, Has.All.Property(nameof(MovementHistory.TurnSequence)).EqualTo(1), "The Turn's first allocation, shared by the whole batch.");
            Assert.That(rows, Has.All.Property(nameof(MovementHistory.FromCampaignMapTileId)).Null);
        }
    }

    [Test, Description("When the Created rows cannot be written, the Forces are rolled back with them, so nothing comes into existence unrecorded.")]
    public async Task ShouldCreateNothingWhenTheCreatedRowsCannotBeWrittenAsync()
    {
        int tileId = await FirstTileIdAsync();
        int unitCountBefore = await CountUnitsAsync();

        // An allocator that answers for a Turn that does not exist. The Forces' insert succeeds, and then the rows' insert breaks the Player Turn foreign key - the second of the two saves failing.
        IPlayerTurnService lyingAllocator = Substitute.For<IPlayerTurnService>();

        lyingAllocator.AllocateSequenceAsync(Arg.Any<AscendantEmpireContext>(), _MISSING_PLAYER_TURN_ID, 1, Arg.Any<CancellationToken>())
            .Returns(ServiceResult<TurnSequenceBlock>.Succeed(new TurnSequenceBlock(1, 1)));

        var writer = new ForceLifecycleWriter(NullLogger<ForceLifecycleWriter>.Instance, lyingAllocator);

        ServiceResultBase result;

        await using (AscendantEmpireContext context = await DbFactory.CreateDbContextAsync())
        {
            result = await writer.CreateAsync(context, [NewSettler(tileId)], _MISSING_PLAYER_TURN_ID);
        }

        using (Assert.EnterMultipleScope())
        {
            Assert.That(result.HasSucceeded, Is.False);
            Assert.That(await CountUnitsAsync(), Is.EqualTo(unitCountBefore), "The first save was rolled back along with the second.");
            Assert.That(await LoadHistoryAsync(), Is.Empty);
        }
    }

    [Test, Description("A destroyed Force's rows keep its id after the hard delete, because the column has no foreign key to clear it or block the delete.")]
    public async Task ShouldKeepAForcesIdOnItsHistoryAfterItIsDestroyedAsync()
    {
        int playerTurnId = await OpenTurnAsync();
        int tileId = await FirstTileIdAsync();

        UnitInstance created = NewSettler(tileId);

        await using (AscendantEmpireContext context = await DbFactory.CreateDbContextAsync())
        {
            ServiceResultBase createResult = await RealWriter().CreateAsync(context, [created], playerTurnId);

            Assert.That(createResult.HasSucceeded, Is.True, () => string.Join("; ", createResult.Errors));
        }

        await using (AscendantEmpireContext context = await DbFactory.CreateDbContextAsync())
        {
            UnitInstance doomed = await context.UnitInstances.SingleAsync(u => u.Id == created.Id);

            ServiceResultBase destroyResult = await RealWriter().DestroyAsync(context, doomed, playerTurnId);

            Assert.That(destroyResult.HasSucceeded, Is.True, () => string.Join("; ", destroyResult.Errors));

            await context.SaveChangesAsync();
        }

        List<MovementHistory> rows = await LoadHistoryAsync();

        using (Assert.EnterMultipleScope())
        {
            Assert.That(await CountUnitsAsync(u => u.Id == created.Id), Is.Zero, "Destruction is a hard delete.");
            Assert.That(rows.Select(row => row.EventType), Is.EqualTo(new[] { ForceHistoryEventType.Created, ForceHistoryEventType.Destroyed }));
            Assert.That(rows, Has.All.Property(nameof(MovementHistory.UnitInstanceId)).EqualTo(created.Id), "The path of a dead Force is still one path.");
            Assert.That(rows.Select(row => row.TurnSequence), Is.EqualTo(new[] { 1, 2 }), "Both feeds draw from the one Turn counter, in order.");
        }
    }

    [TestCase(ForceHistoryEventType.Moved, false, Description = "A Moved row that does not say where the Force came from is refused, because the fold computes exit directions from it.")]
    [TestCase(ForceHistoryEventType.Created, true, Description = "A Created row naming a tile the Force came from is refused, since a Force that has just appeared stepped off nothing.")]
    public async Task ShouldRejectARowWhoseFromTileDoesNotMatchItsEventTypeAsync(ForceHistoryEventType eventType, bool hasFromTile)
    {
        int playerTurnId = await OpenTurnAsync();

        await using AscendantEmpireContext context = await DbFactory.CreateDbContextAsync();

        UnitInstance startingForce = await context.UnitInstances.Where(u => u.GameId == _gameId).OrderBy(u => u.Id).FirstAsync();

        // Written straight to the table rather than through the writer, which never builds a row like this. The constraint is what stops anything else that tries.
        context.MovementHistory.Add(new MovementHistory
        {
            GameId = _gameId,
            UnitInstanceId = startingForce.Id,
            PlayerId = startingForce.PlayerId,
            UnitTemplateId = startingForce.UnitTemplateId,
            EventType = eventType,
            FromCampaignMapTileId = hasFromTile ? startingForce.CampaignMapTileId : null,
            CampaignMapTileId = startingForce.CampaignMapTileId,
            PlayerTurnId = playerTurnId,
            TurnSequence = 1
        });

        DbUpdateException? exception = Assert.ThrowsAsync<DbUpdateException>(() => context.SaveChangesAsync());

        Assert.That(exception!.InnerException?.Message, Does.Contain("CK_MovementHistory_FromTileOnlyWhenMoved"));
    }

    [Test, Description("A developer spawn during play is recorded against the open Turn, so a Unit spawned mid-game appears in the intelligence feeds like any other.")]
    public async Task ShouldRecordADeveloperSpawnAgainstTheOpenTurnAsync()
    {
        // Spawning looks the Game up through a filter that requires it to be in progress.
        await StartGameAsync(_gameId);

        int playerTurnId = await OpenTurnAsync();
        int[] unitIdsBefore = await UnitIdsAsync();

        ServiceResultBase result = await BuildMediator().Send(new SpawnUnitRequest(
            GameId: _gameId,
            PlayerId: _playerId,
            XCoord: 2,
            YCoord: 1,
            UnitTemplateId: _settlerTemplateId,
            InitialStrength: _SETTLER_SIZE,
            HealthyCount: _SETTLER_SIZE,
            InjuredCount: 0));

        Assert.That(result.HasSucceeded, Is.True, () => $"{string.Join("; ", result.Errors)} Logged exception: {_spawnLogger.LastException}");

        int spawnedUnitId = (await UnitIdsAsync()).Except(unitIdsBefore).Single();
        MovementHistory row = (await LoadHistoryAsync()).Single();

        using (Assert.EnterMultipleScope())
        {
            Assert.That(row.EventType, Is.EqualTo(ForceHistoryEventType.Created));
            Assert.That(row.UnitInstanceId, Is.EqualTo(spawnedUnitId));
            Assert.That(row.PlayerTurnId, Is.EqualTo(playerTurnId));
        }
    }

    [Test, Description("A developer spawn into a Game with no open Turn is placed without a row, exactly as the starting armies are.")]
    public async Task ShouldSpawnWithoutARowWhenNoTurnIsOpenAsync()
    {
        await StartGameAsync(_gameId);

        ServiceResultBase result = await BuildMediator().Send(new SpawnUnitRequest(
            GameId: _gameId,
            PlayerId: _playerId,
            XCoord: 2,
            YCoord: 1,
            UnitTemplateId: _settlerTemplateId,
            InitialStrength: _SETTLER_SIZE,
            HealthyCount: _SETTLER_SIZE,
            InjuredCount: 0));

        using (Assert.EnterMultipleScope())
        {
            Assert.That(result.HasSucceeded, Is.True, () => $"{string.Join("; ", result.Errors)} Logged exception: {_spawnLogger.LastException}");
            Assert.That(await LoadHistoryAsync(), Is.Empty);
        }
    }

    // ---------------------------------------------------------------------

    /// <summary>The writer over the real allocator, on the per-test database.</summary>
    private ForceLifecycleWriter RealWriter()
    {
        return new ForceLifecycleWriter(NullLogger<ForceLifecycleWriter>.Instance, new PlayerTurnService(NullLogger<PlayerTurnService>.Instance, DbFactory));
    }

    /// <summary>
    /// A mediator wired the way the spawn handler needs it, with the real turn services so the open Turn is genuinely looked up.
    /// </summary>
    private IMediator BuildMediator()
    {
        // GetGameContextHandler delegates its query to IGameContextService, which AddMediator does not register, so that goes in by hand.
        ServiceProvider provider = new ServiceCollection()
            .AddSingleton(DbFactory)
            .AddScoped(_ => DbFactory.CreateDbContext())
            .AddScoped<IGameContextService, GameContextService>()
            .AddSingleton<ICampaignTurnService>(new CampaignTurnService(NullLogger<CampaignTurnService>.Instance, DbFactory))
            .AddScoped<IPlayerTurnService, PlayerTurnService>()
            .AddScoped<IForceLifecycleWriter, ForceLifecycleWriter>()
            .AddMediator(typeof(GetPlayersHandler).Assembly)
            .AddLogging()
            .AddSingleton<ILogger<SpawnUnitHandler>>(_spawnLogger)
            .BuildServiceProvider();

        return provider.GetRequiredService<IMediator>();
    }

    /// <summary>Opens the first player's Turn in the Game's campaign lane and returns its id.</summary>
    private async Task<int> OpenTurnAsync()
    {
        await using AscendantEmpireContext context = await DbFactory.CreateDbContextAsync();

        var playerTurnService = new PlayerTurnService(NullLogger<PlayerTurnService>.Instance, DbFactory);

        ServiceResult<int> result = await playerTurnService.OpenPlayerTurnAsync(context, _campaignQueueId, _playerId, roundNumber: 1);

        Assert.That(result.HasSucceeded, Is.True, () => string.Join("; ", result.Errors));

        return result.SuccessValue;
    }

    private async Task<int> FirstTileIdAsync()
    {
        await using AscendantEmpireContext context = await DbFactory.CreateDbContextAsync();

        return await context.CampaignMaps.Where(t => t.GameId == _gameId).OrderBy(t => t.CampaignMapId).Select(t => t.CampaignMapId).FirstAsync();
    }

    /// <summary>A new, unsaved settler belonging to the first player.</summary>
    private UnitInstance NewSettler(int tileId)
    {
        return new UnitInstance
        {
            GameId = _gameId,
            PlayerId = _playerId,
            CampaignMapTileId = tileId,
            UnitTemplateId = _settlerTemplateId,
            InitialStrength = _SETTLER_SIZE,
            HealthyCount = _SETTLER_SIZE,
            InjuredCount = 0
        };
    }

    private async Task<int> CountUnitsAsync()
    {
        return await CountUnitsAsync(u => true);
    }

    private async Task<int> CountUnitsAsync(System.Linq.Expressions.Expression<Func<UnitInstance, bool>> predicate)
    {
        await using AscendantEmpireContext context = await DbFactory.CreateDbContextAsync();

        return await context.UnitInstances.Where(u => u.GameId == _gameId).CountAsync(predicate);
    }

    private async Task<int[]> UnitIdsAsync()
    {
        await using AscendantEmpireContext context = await DbFactory.CreateDbContextAsync();

        return await context.UnitInstances.Where(u => u.GameId == _gameId).Select(u => u.Id).ToArrayAsync();
    }

    private async Task<List<MovementHistory>> LoadHistoryAsync()
    {
        await using AscendantEmpireContext context = await DbFactory.CreateDbContextAsync();

        return await context.MovementHistory.Where(h => h.GameId == _gameId).OrderBy(h => h.Id).ToListAsync();
    }
}
