using AscendantEmpire.Application.Common;
using AscendantEmpire.Application.Data;
using AscendantEmpire.Application.Features.Game.Handlers;
using AscendantEmpire.Application.Features.Players.Handlers;
using AscendantEmpire.Application.Features.Turns.Handlers;
using AscendantEmpire.BackendContracts.Turns.CreateCampaignTurnQueue;
using AscendantEmpire.Domain.Entities.Battles;
using AscendantEmpire.Domain.Entities.Turns;
using AscendantEmpire.Domain.Enums.Maps;
using AscendantEmpire.Domain.Enums.Turns;

using MediatR;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;

namespace AscendantEmpire.Tests.Features.Turns.Handlers;

/// <summary>
/// Integration tests for <see cref="CreateBattleTurnQueueHandler"/>.
/// </summary>
/// <remarks>
/// The handler writes the Battle, its participants and the turn queue inside an explicit transaction and leans on the game's foreign keys, so these tests
/// need a real SQL Server. The validator's own rules are covered without a database in <see cref="CreateBattleTurnQueueRequestValidatorTests"/>.
/// </remarks>
[TestFixture]
public class CreateBattleTurnQueueHandlerTests : GameSqlServerTestBase
{
    private CreateBattleTurnQueueHandler _handler = null!;

    [SetUp]
    public void SetUp()
    {
        // Build a real IMediator wired with the handlers that CreateBattleTurnQueueHandler depends on.
        // DbFactory is initialised by SqlServerTestBase before this SetUp runs.
        ServiceProvider sp = new ServiceCollection()
            .AddSingleton(DbFactory)
            .AddMediatR(cfg => cfg.RegisterServicesFromAssemblyContaining<GetPlayersHandler>())
            .AddLogging()
            .BuildServiceProvider();

        IMediator mediator = sp.GetRequiredService<IMediator>();
        _handler = new CreateBattleTurnQueueHandler(new NullLogger<CreateBattleTurnQueueHandler>(), DbFactory, mediator);
    }

    private async Task SetupGameVariablesAsync(int mapWidth, int mapHeight)
    {
        await SeedGameAsync(mapWidth, mapHeight, 2);
    }

    [Test, Description("A player id not in the game fails the whole request, and the error names the offending ids.")]
    public async Task ShouldFailIfPlayerIsNotInGameAsync()
    {
        // Setup the Game
        await SetupGameVariablesAsync(mapWidth: 2, mapHeight: 2);

        SetupGameRequest request = new(GalleryMapId: _galleryIndexId, GameName: "Queue Test Game", NumberOfPlayers: 2, BattleMapSize: 50, GenerationSeed: null);

        ServiceResultBase.ServiceResult<int> result = await _service.SetupGameAsync(request, CancellationToken.None);

        Assert.That(result.HasSucceeded, Is.True, $"SetupGameAsync failed: {string.Join(", ", result.Errors)}\nLogged exception: {_capturingLogger.LastException}");

        int gameId = result.SuccessValue;

        // Attempt to create a Battle Turn Queue with an invalid Player Id (3, which is not in the game).
        CreateBattleTurnQueueRequest createQueueRequest = new(GameId: gameId, CampaignMapTileId: 1, PlayerIdsInTurnOrder: [1, 3]);
        ServiceResultBase.ServiceResult<FirstPlayerInRound> createQueueResult = await _handler.Handle(createQueueRequest, CancellationToken.None);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(createQueueResult.HasSucceeded, Is.False, $"CreateBattleTurnQueueHandler should fail when a Player is not in the game. Errors: {string.Join(", ", createQueueResult.Errors)}");
            Assert.That(createQueueResult.Errors, Has.Some.EqualTo("One or more Players are not active in this Game. Player Ids: 3."));
        }
    }

    [Test, Description("Starting a battle writes four tables at once, and a half-written set would leave it unplayable.")]
    public async Task ShouldSucceedAndPersistAllTablesCorrectlyAsync()
    {
        await SetupGameVariablesAsync(mapWidth: 2, mapHeight: 2);

        SetupGameRequest setupRequest = new(GalleryMapId: _galleryIndexId, GameName: "Queue Test Game", NumberOfPlayers: 2, BattleMapSize: 50, GenerationSeed: null);

        ServiceResultBase.ServiceResult<int> setupResult = await _service.SetupGameAsync(setupRequest, CancellationToken.None);

        Assert.That(setupResult.HasSucceeded, Is.True, $"SetupGameAsync failed: {string.Join(", ", setupResult.Errors)}\nLogged exception: {_capturingLogger.LastException}");

        int gameId = setupResult.SuccessValue;
        const int campaignMapTileId = 1;

        int[] playerIds = [1, 2];
        CreateBattleTurnQueueRequest createQueueRequest = new(GameId: gameId, CampaignMapTileId: campaignMapTileId, PlayerIdsInTurnOrder: playerIds);

        ServiceResultBase.ServiceResult<FirstPlayerInRound> handlerResult = await _handler.Handle(createQueueRequest, CancellationToken.None);

        Assert.That(handlerResult.HasSucceeded, Is.True, $"Handler failed unexpectedly. Errors: {string.Join(", ", handlerResult.Errors)}\nLogged exception: {_capturingLogger.LastException}");

        FirstPlayerInRound firstPlayer = handlerResult.SuccessValue;

        using (Assert.EnterMultipleScope())
        {
            Assert.That(firstPlayer.PlayerId, Is.EqualTo(playerIds[0]), "First player in round should be the first player in the requested turn order.");
            Assert.That(firstPlayer.RoundNumber, Is.GreaterThan(0), "Round number should be greater than zero.");
            Assert.That(firstPlayer.TurnQueueId, Is.GreaterThan(0), "TurnQueueId should be a valid persisted Id.");
        }

        // Assert — verify persisted state by reading directly from the DB
        await using AscendantEmpireContext verifyCtx = await DbFactory.CreateDbContextAsync();

        // Battle row
        Battle? battle = await verifyCtx.Battles
            .FirstOrDefaultAsync(b => b.GameId == gameId && b.CampaignMapTileId == campaignMapTileId);

        Assert.That(battle, Is.Not.Null, "A Battle row should have been persisted.");
        using (Assert.EnterMultipleScope())
        {
            Assert.That(battle!.CommencedAtRoundNumber, Is.EqualTo(firstPlayer.RoundNumber), "Battle.CommencedAtRoundNumber should match the returned round number.");
            Assert.That(battle.ConcludedAtRoundNumber, Is.Null, "Battle should not be concluded.");
            Assert.That(battle.GlobalBattleName, Is.Not.Null.And.Not.Empty, "Battle should have a generated name.");
        }

        // BattleParticipant rows
        List<BattleParticipant> battleParticipants = await verifyCtx.BattleParticipants
            .Where(bp => bp.BattleId == battle.Id)
            .OrderBy(bp => bp.PlayerId)
            .ToListAsync();

        Assert.That(battleParticipants, Has.Count.EqualTo(playerIds.Length), "One BattleParticipant row should exist per requested player.");
        using (Assert.EnterMultipleScope())
        {
            Assert.That(battleParticipants.Select(bp => bp.PlayerId), Is.EquivalentTo(playerIds), "BattleParticipant player ids should match the requested player ids.");
            Assert.That(battleParticipants.All(bp => bp.IsStillInBattle), Is.True, "All BattleParticipants should still be in the battle.");
            Assert.That(battleParticipants.All(bp => bp.JoinedAtRoundNumber == firstPlayer.RoundNumber), Is.True, "All BattleParticipants should have joined at the commenced round.");
        }

        // TurnQueue row
        TurnQueue? turnQueue = await verifyCtx.TurnQueues
            .FirstOrDefaultAsync(tq => tq.Id == firstPlayer.TurnQueueId);

        Assert.That(turnQueue, Is.Not.Null, "A TurnQueue row should have been persisted.");
        using (Assert.EnterMultipleScope())
        {
            Assert.That(turnQueue!.BattleId, Is.EqualTo(battle.Id), "TurnQueue.BattleId should reference the persisted Battle.");
            Assert.That(turnQueue.GameId, Is.EqualTo(gameId), "TurnQueue.GameId should match the requested Game.");
            Assert.That(turnQueue.QueueType, Is.EqualTo(TurnQueueType.Battle), "TurnQueue should be of type Battle.");
            Assert.That(turnQueue.IsCompleted, Is.False, "TurnQueue should not be marked as completed.");
            Assert.That(turnQueue.CurrentRoundNumber, Is.EqualTo(firstPlayer.RoundNumber), "TurnQueue.CurrentRoundNumber should match the commenced round.");
        }

        // TurnQueueParticipant rows
        List<TurnQueueParticipant> turnQueueParticipants = await verifyCtx.TurnQueueParticipants
            .Where(tqp => tqp.TurnQueueId == turnQueue.Id)
            .OrderBy(tqp => tqp.TurnOrder)
            .ToListAsync();

        Assert.That(turnQueueParticipants, Has.Count.EqualTo(playerIds.Length), "One TurnQueueParticipant row should exist per requested player.");
        using (Assert.EnterMultipleScope())
        {
            Assert.That(turnQueueParticipants.Select(tqp => tqp.PlayerId), Is.EqualTo(playerIds), "TurnQueueParticipant player ids should match the requested turn order.");
            Assert.That(turnQueueParticipants.Select(tqp => tqp.TurnOrder), Is.EqualTo(Enumerable.Range(1, playerIds.Length)), "TurnOrder should be sequential starting from 1.");
            Assert.That(turnQueueParticipants.All(tqp => tqp.IsActiveInLane), Is.True, "All TurnQueueParticipants should be active in the lane.");
            Assert.That(turnQueueParticipants.All(tqp => tqp.Status == TurnQueueParticipantStatus.Pending), Is.True, "All TurnQueueParticipants should have a Pending status.");
        }
    }

    [Test, Description("One tile may host only one unconcluded battle, so a second request for that tile is refused.")]
    public async Task ShouldFailIfActiveBattleExistsForCampaignMapTileIdAsync()
    {
        // Setup the Game
        await SetupGameVariablesAsync(mapWidth: 2, mapHeight: 2);

        SetupGameRequest request = new(GalleryMapId: _galleryIndexId, GameName: "Queue Test Game", NumberOfPlayers: 2, BattleMapSize: 50, GenerationSeed: null);

        ServiceResultBase.ServiceResult<int> result = await _service.SetupGameAsync(request, CancellationToken.None);

        Assert.That(result.HasSucceeded, Is.True, $"SetupGameAsync failed: {string.Join(", ", result.Errors)}\nLogged exception: {_capturingLogger.LastException}");

        int gameId = result.SuccessValue;

        // Insert an active Battle row directly so the handler sees an existing active Battle for the // tile without going through a second handler call
        // (which would use a separate context and risk FK constraint failures from partially-committed state).
        await using (AscendantEmpireContext seedCtx = await DbFactory.CreateDbContextAsync())
        {
            DateTimeOffset now = DateTimeOffset.UtcNow;
            seedCtx.Battles.Add(new Battle
            {
                GameId = gameId,
                CampaignMapTileId = 1,
                GlobalBattleName = "Seeded Active Battle",
                CommencedAtRoundNumber = 1,
                OriginalTileStatus = TileOwnershipStatus.Unclaimed,
                OriginalTileOwnerId = null,
                ConcludedAtRoundNumber = null,
                VictorId = null,
                CreatedAt = now,
                UpdatedAt = now,
                RowVersion = []
            });
            await seedCtx.SaveChangesAsync();
        }

        CreateBattleTurnQueueRequest createQueueRequest = new(GameId: gameId, CampaignMapTileId: 1, PlayerIdsInTurnOrder: [1, 2]);
        ServiceResultBase.ServiceResult<FirstPlayerInRound> createSecondResult = await _handler.Handle(createQueueRequest, CancellationToken.None);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(createSecondResult.HasSucceeded, Is.False, $"CreateBattleTurnQueueHandler should fail when an active Battle exists for the Campaign Map Tile. Errors: {string.Join(", ", createSecondResult.Errors)}");
            Assert.That(createSecondResult.Errors, Has.Some.EqualTo("An active Battle already exists for this Campaign Map Tile."));
        }
    }
}
